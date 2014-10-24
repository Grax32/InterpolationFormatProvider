using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Grax.Text
{
    public class InterpolationFormatProvider : IFormatProvider, ICustomFormatter
    {
        static readonly Dictionary<Type, Func<object, string, object>> _fetchers = new Dictionary<Type, Func<object, string, object>>();
        static readonly Expression<Func<object>> _getValueDictionaryFunction = () => GetDictionaryValueOrDefault<object>(null, null);
        static readonly MethodInfo _getValueDictionaryFunctionMethodInfo = (_getValueDictionaryFunction.Body as MethodCallExpression).Method.GetGenericMethodDefinition();
        const string IfpPrefix = "i.";

        readonly object _instance;

        public InterpolationFormatProvider() { }
        public InterpolationFormatProvider(object instance) { _instance = instance; }

        public object GetFormat(Type formatType)
        {
            return (formatType == typeof(ICustomFormatter)) ? this : null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg == null) { return ""; }

            var forceIfp = (format ?? "").StartsWith(IfpPrefix);

            if (arg is IFormattable && arg != _instance && !forceIfp)
            {
                format = string.IsNullOrEmpty(format) ? "{0}" : "{0:" + format + "}";
                return string.Format(format, arg);
            }
            else
            {
                if (forceIfp) { format = format.Substring(IfpPrefix.Length); }

                return GetPropertyValueAndFormat(arg, format, formatProvider);
            }
        }

        private static string GetPropertyValueAndFormat(object arg, string propertyName, IFormatProvider formatProvider)
        {
            object value;
            string format = null;
            if (propertyName == null)
            {
                return arg.ToString();
            }
            else if (propertyName.Contains(":"))
            {
                var firstColon = propertyName.IndexOf(':');

                format = "{0" + propertyName.Substring(firstColon) + "}";
                propertyName = propertyName.Substring(0, firstColon);

                value = GetPropertyValue(arg, propertyName);
            }
            else
            {
                value = GetPropertyValue(arg, propertyName);
            }

            if (value is IFormattable)
            {
                formatProvider = null;
            }

            return string.Format(formatProvider, format ?? "{0}", value ?? "");
        }

        private static object GetPropertyValue(object arg, string propertyName)
        {
            var type = arg.GetType();

            Func<object, string, object> fetcher;
            if (_fetchers.TryGetValue(type, out fetcher))
            {
                return fetcher(arg, propertyName);
            }
            else
            {
                var argExpresson = Expression.Parameter(typeof(object), "arg");
                var argExpressionOfType = Expression.Convert(argExpresson, type);

                var propertyNameExpression = Expression.Parameter(typeof(string), "propertyName");
                Expression body;

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments().First() == typeof(string))
                {
                    var method = _getValueDictionaryFunctionMethodInfo.MakeGenericMethod(type.GetGenericArguments()[1]);

                    body = Expression.Call(null, method, argExpressionOfType, propertyNameExpression);
                }
                else
                {
                    var propertiesAndFields = type
                        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Select(v => new { Name = v.Name, Expression = Expression.Convert(Expression.Property(argExpressionOfType, v.Name), typeof(object)) })
                        .Union(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Select(v => new { Name = v.Name, Expression = Expression.Convert(Expression.Field(argExpressionOfType, v.Name), typeof(object)) }));

                    var defaultSwitchBlock = Expression.Convert(Expression.Constant("", typeof(string)), typeof(object));

                    body = Expression.Switch(propertyNameExpression,
                        defaultSwitchBlock,
                        propertiesAndFields.Select(v => Expression.SwitchCase(v.Expression, Expression.Constant(v.Name))).ToArray());
                }

                var fetcherExpression = Expression.Lambda<Func<object, string, object>>(body, argExpresson, propertyNameExpression);
                fetcher = fetcherExpression.Compile();
                _fetchers[type] = fetcher;
            }

            return fetcher(arg, propertyName);
        }

        static object GetDictionaryValueOrDefault<T>(Dictionary<string, T> dictionary, string key)
        {
            var result = default(T);
            if (!dictionary.TryGetValue(key, out result))
            {
                return null;
            }

            return result;
        }
    }
}