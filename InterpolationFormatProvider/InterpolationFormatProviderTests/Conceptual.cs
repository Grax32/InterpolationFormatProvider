using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Grax.Text
{
    public static class InterpolationExtensions
    {
        static Expression<Func<string>> _concatExpr = () => string.Concat(new string[] { });
        static MethodInfo _stringConcatMethod = ((MethodCallExpression)_concatExpr.Body).Method;

        static readonly Expression<Func<object>> _getValueDictionaryFunction = () => GetDictionaryValueToString<object>(null, null, null);
        static readonly MethodInfo _getValueDictionaryFunctionMethodInfo = (_getValueDictionaryFunction.Body as MethodCallExpression).Method.GetGenericMethodDefinition();

        static readonly Expression<Func<object>> _getFetcherExpression = () => GetFetcherExpression<object>("", null);
        static readonly MethodInfo _GetFetcherExpressionMethodInfo = (_getFetcherExpression.Body as MethodCallExpression).Method.GetGenericMethodDefinition();

        public static string ToInterpolatedString(this string pattern, params object[] args)
        {
            return new InterpolationFormatProvider().Format(pattern, args, null);
        }

        public static Func<T1, string> ToCompiledFormat<T1>(this string pattern, T1 arg1) { return (Func<T1, string>)ToCompiledFormat(pattern, new[] { typeof(T1) }); }
        public static Func<T1, T2, string> ToCompiledFormat<T1, T2>(this string pattern, T1 arg1, T2 arg2) { return (Func<T1, T2, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2) }); }
        public static Func<T1, T2, T3, string> ToCompiledFormat<T1, T2, T3>(this string pattern, T1 arg1, T2 arg2, T3 arg3) { return (Func<T1, T2, T3, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3) }); }
        public static Func<T1, T2, T3, T4, string> ToCompiledFormat<T1, T2, T3, T4>(this string pattern, T1 arg1, T2 arg2, T3 arg3, T4 arg4) { return (Func<T1, T2, T3, T4, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }); }
        public static Func<T1, T2, T3, T4, T5, string> ToCompiledFormat<T1, T2, T3, T4, T5>(this string pattern, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) { return (Func<T1, T2, T3, T4, T5, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }); }
        public static Func<T1, T2, T3, T4, T5, T6, string> ToCompiledFormat<T1, T2, T3, T4, T5, T6>(this string pattern, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) { return (Func<T1, T2, T3, T4, T5, T6, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) }); }
        public static Func<T1, T2, T3, T4, T5, T6, T7, string> ToCompiledFormat<T1, T2, T3, T4, T5, T6, T7>(this string pattern, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) { return (Func<T1, T2, T3, T4, T5, T6, T7, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7) }); }
        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, string> ToCompiledFormat<T1, T2, T3, T4, T5, T6, T7, T8>(this string pattern, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) { return (Func<T1, T2, T3, T4, T5, T6, T7, T8, string>)ToCompiledFormat(pattern, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8) }); }

        private static object ToCompiledFormat(this string pattern, Type[] argumentTypes)
        {
            var parameters = argumentTypes.Select(v => Expression.Parameter(v)).ToArray();

            var patterns = pattern.Split(new[] { "{" }, StringSplitOptions.None);

            var expressions = new List<Expression>();

            AddStringExpressionIfNotBlank(expressions, patterns[0]);

            foreach (var p in patterns.Skip(1))
            {
                if (p.Length == 0)
                {
                    expressions.Add(Expression.Constant("{"));
                }
                else
                {
                    var endChar = p.IndexOf("}");

                    var item = p.Substring(0, endChar);
                    var itemName = item;
                    var itemFormat = "";
                    var itemNumber = -1;

                    if (item.IndexOf(':') != -1)
                    {
                        var colonChar = item.IndexOf(':');
                        itemName = item.Substring(0, colonChar);
                        itemFormat = item.Substring(colonChar + 1);
                    }

                    int.TryParse(itemName, out itemNumber);

                    if (itemNumber >= argumentTypes.Length)
                    {
                        throw new ArgumentException("No type was specified for marker " + itemNumber.ToString(), "argumentTypes");
                    }

                    var fetcher = GetFetcherExpression(argumentTypes[itemNumber], itemFormat, parameters[itemNumber]);

                    expressions.Add(fetcher);
                    AddStringExpressionIfNotBlank(expressions, p.Substring(endChar + 1));
                }
            }

            expressions = MergeStringConstants(expressions);

            var arrayOfStrings = Expression.NewArrayInit(typeof(string), expressions);

            var func = Expression.Call(null, _stringConcatMethod, arrayOfStrings);
            var expression = Expression.Lambda(func, parameters);
            return expression.Compile();
        }

        static List<Expression> MergeStringConstants(List<Expression> expressions)
        {
            var result = new List<Expression>();
            var workExpr = expressions.ToArray();
            var stringCache = "";

            for (var i = 0; i < workExpr.Length; i++)
            {
                var thisExpr = workExpr[i];
                var constantExpr = thisExpr as ConstantExpression;
                if (constantExpr == null)
                {
                    if (!string.IsNullOrEmpty(stringCache))
                    {
                        result.Add(Expression.Constant(stringCache));
                        stringCache = null;
                    }

                    result.Add(thisExpr);
                }
                else
                {
                    stringCache += (string)constantExpr.Value;
                }
            }

            if (!string.IsNullOrEmpty(stringCache))
            {
                result.Add(Expression.Constant(stringCache));
                stringCache = null;
            }

            return result;
        }

        static void AddStringExpressionIfNotBlank(List<Expression> expressions, string constantString)
        {
            if (!string.IsNullOrEmpty(constantString))
            {
                expressions.Add(Expression.Constant(constantString));
            }
        }

        internal static Expression GetFetcherExpression(Type type, string format, ParameterExpression parm)
        {
            return (Expression)_GetFetcherExpressionMethodInfo.MakeGenericMethod(type).Invoke(null, new object[] { format, parm });
        }

        internal static Expression GetFetcherExpression<T>(string format, ParameterExpression argExpressionOfType)
        {
            var type = typeof(T);
            //var argExpressionOfType = Expression.Parameter(type, "arg");
            var formatExpression = Expression.Constant(format);

            Expression body;

            if (type == typeof(string))
            {
                if (string.IsNullOrEmpty(format))
                {
                    body = argExpressionOfType;
                }
                else
                {
                    throw new ArgumentException("Format must be blank for argument of type string", "format");
                }
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments().First() == typeof(string))
            {
                var method = _getValueDictionaryFunctionMethodInfo.MakeGenericMethod(type.GetGenericArguments()[1]);

                body = Expression.Call(null, method, argExpressionOfType, formatExpression);
            }
            else if (typeof(IFormattable).IsAssignableFrom(type))
            {
                body = Expression.Call(argExpressionOfType, type.GetMethod("ToString", new Type[] { typeof(string), typeof(IFormatProvider) }), formatExpression, Expression.Constant(null, typeof(IFormatProvider)));
            }
            else
            {
                var propertyOrField = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(v => v.Name == format)
                    .Select(v => Expression.Property(argExpressionOfType, v.Name))
                    .Union(
                        type
                            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                            .Where(v => v.Name == format)
                            .Select(v => Expression.Field(argExpressionOfType, v.Name)))
                    .FirstOrDefault();

                if (propertyOrField != null)
                {
                    body = propertyOrField;
                }
                else
                {
                    throw new FormatException(string.Format("{0} is not an acceptable format for type {1}", format, type.Name));
                }
            }

            return body;
        }

        static string GetDictionaryValueToString<T>(Dictionary<string, T> dictionary, string key, string format)
        {
            var result = default(T);
            if (!dictionary.TryGetValue(key, out result))
            {
                return "";
            }

            return string.Format("{0:" + format + "}");
        }
    }
}
