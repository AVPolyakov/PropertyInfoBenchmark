using System;
using System.Linq.Expressions;

namespace PropertyInfoBenchmark
{
    public interface IPropertyMetadata<in T, out TProperty>
    {
        string PropertyName { get; }
        Func<T, TProperty> Func { get; }
    }

    public class PropertyMetadata<T, TProperty> : IPropertyMetadata<T, TProperty>
    {
        public string PropertyName { get; }
        public Func<T, TProperty> Func { get; }

        internal PropertyMetadata(string propertyName, Func<T, TProperty> func)
        {
            PropertyName = propertyName;
            Func = func;
        }
    }

    public static class PropertyMetadata
    {
        public static IPropertyMetadata<T, TProperty> Metadata<T, TProperty>(this T protoType,
            Expression<Func<T, TProperty>> expression)
        {
            var propertyName = ((MemberExpression) expression.Body).Member.Name;
            return new PropertyMetadata<T, TProperty>(propertyName, expression.Compile());
        }
    }
}