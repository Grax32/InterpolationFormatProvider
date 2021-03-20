using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Grax32
{
    public class InterpolationFormatProvider : IFormatProvider, ICustomFormatter
    {
        private static readonly Dictionary<Type, Func<object, string, object>> Fetchers =
            new Dictionary<Type, Func<object, string, object>>();

        private static readonly Expression<Func<object>> GetValueDictionaryFunction =
            () => GetDictionaryValueOrDefault<object>(null, null);

        private static readonly MethodInfo GetValueDictionaryFunctionMethodInfo =
            (GetValueDictionaryFunction.Body as MethodCallExpression)?.Method.GetGenericMethodDefinition();

        private const string IfpPrefix = "i.";
        private readonly object _instance;

        public InterpolationFormatProvider()
        {
        }

        public InterpolationFormatProvider(object instance)
        {
            _instance = instance;
        }

        public object GetFormat(Type formatType)
        {
            return (formatType == typeof(ICustomFormatter)) ? this : null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg == null)
            {
                return "";
            }

            if (formatProvider == null)
            {
                formatProvider = this;
            }

            var forceIfp = (format ?? "").StartsWith(IfpPrefix);

            if (arg is IFormattable && arg != _instance && !forceIfp)
            {
                format = string.IsNullOrEmpty(format) ? "{0}" : "{0:" + format + "}";
                return string.Format(format, arg);
            }

            if (forceIfp)
            {
                format = format.Substring(IfpPrefix.Length);
            }

            return GetPropertyValueAndFormat(arg, format, formatProvider);
        }

        private static string GetPropertyValueAndFormat(object arg, string propertyName, IFormatProvider formatProvider)
        {
            object value;
            string format = null;

            if (propertyName == null)
            {
                return arg.ToString();
            }

            if (propertyName.Contains(":"))
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

        private static bool ImplementsIDictionaryStringSomething(Type type)
        {
            return type.GetInterfaces().Any(v =>
                v.IsGenericType &&
                v.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                v.GetGenericArguments().First() == typeof(string)
            );
        }

        private static Type FetchGenericArgumentType(Type type)
        {
            return type.GetInterfaces()
                .Where(v =>
                    v.IsGenericType &&
                    v.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                    v.GetGenericArguments().First() == typeof(string)
                )
                .Select(v => v.GetGenericArguments()[1])
                .OrderBy(v =>
                    v == typeof(object)
                        ? 99999
                        : 1) // if more than one interface matches, prefer one where the value is not of type object
                .First();
        }

        private static object GetPropertyValue(object arg, string propertyName)
        {
            var type = arg.GetType();

            if (Fetchers.TryGetValue(type, out var fetcher))
            {
                return fetcher(arg, propertyName);
            }

            var argExpression = Expression.Parameter(typeof(object), "arg");
            var argExpressionOfType = Expression.Convert(argExpression, type);

            var propertyNameExpression = Expression.Parameter(typeof(string), "propertyName");
            Expression body;

            if (ImplementsIDictionaryStringSomething(type))
            {
                var propertyType = FetchGenericArgumentType(type);
                var method = GetValueDictionaryFunctionMethodInfo.MakeGenericMethod(propertyType);

                body = Expression.Call(null, method, argExpressionOfType, propertyNameExpression);
            }
            else
            {
                var propertiesAndFields = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(x => x.GetIndexParameters().Length == 0)
                    .Select(v => new
                    {
                        v.Name,
                        Expression = Expression.Convert(Expression.Property(argExpressionOfType, v.Name),
                            typeof(object))
                    })
                    .Union(type
                        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                        .Select(v => new
                        {
                            v.Name,
                            Expression = Expression.Convert(Expression.Field(argExpressionOfType, v.Name),
                                typeof(object))
                        }));

                var defaultSwitchBlock =
                    Expression.Convert(Expression.Constant("", typeof(string)), typeof(object));

                body = Expression.Switch(propertyNameExpression,
                    defaultSwitchBlock,
                    propertiesAndFields
                        .Select(v => Expression.SwitchCase(v.Expression, Expression.Constant(v.Name))).ToArray());
            }

            var fetcherExpression =
                Expression.Lambda<Func<object, string, object>>(body, argExpression, propertyNameExpression);
            
            fetcher = fetcherExpression.Compile();
            
            Fetchers[type] = fetcher;

            return fetcher(arg, propertyName);
        }

        private static object GetDictionaryValueOrDefault<T>(IDictionary<string, T> dictionary, string key)
        {
            if (!dictionary.TryGetValue(key, out var result))
            {
                return null;
            }

            return result;
        }
    }
}