using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using FluentValidation.Internal;

namespace PropertyInfoBenchmark
{
    class Program
    {
        static void Main()
        {
            Test(() => GetPropertyTuple0(new Person {FirstName = "FN1"}, _ => _.FirstName), nameof(GetPropertyTuple0));
            //Test(() => GetPropertyTuple1(new Person {FirstName = "FN1"}, _ => _.FirstName), nameof(GetPropertyTuple1));
            Test(() => GetPropertyTuple2(new Person {FirstName = "FN1"}, _ => _.FirstName), nameof(GetPropertyTuple2));
            Test(() => GetPropertyTuple2(new Person {FirstName = "FN1"}, _expression), nameof(GetPropertyTuple2) + "_cached");
            Test(() => GetPropertyTuple3(new Person {FirstName = "FN1"}, _ => _.FirstName), nameof(GetPropertyTuple3));
            Test(() => GetPropertyTuple4(new Person {FirstName = "FN1"}, _ => _.FirstName), nameof(GetPropertyTuple4));
            Test(() => GetPropertyTuple5(new Person {FirstName = "FN1"}, _ => _.FirstName()), nameof(GetPropertyTuple5));
        }

        private static readonly Expression<Func<Person, string>> _expression = _ => _.FirstName;

        private static void Test(Action action, string testName)
        {
            for (var i = 0; i < 10; i++)
                action();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 1000 * 1000; i++)
                action();
            stopwatch.Stop();
            Console.WriteLine($"{testName} {stopwatch.ElapsedMilliseconds}ms");
        }

        private static Tuple<string, TProperty> GetPropertyTuple0<T, TProperty>(
            T entity, Func<T, TProperty> func)
        {
            return Tuple.Create("FirstName", func(entity));
        }

        private static Tuple<string, TProperty> GetPropertyTuple1<T, TProperty>(
            T entity, Expression<Func<T, TProperty>> expression)
        {
            var propertyName = ((MemberExpression) expression.Body).Member.Name;
            var propertyValue = expression.Compile()(entity);
            return Tuple.Create(propertyName, propertyValue);//qwe
        }

        private static Tuple<string, TProperty> GetPropertyTuple2<T, TProperty>(
            T entity, Expression<Func<T, TProperty>> expression)
        {
            var memberInfo = ((MemberExpression) expression.Body).Member;
            var propertyName = memberInfo.Name;
            var propertyValue = (TProperty) ((PropertyInfo) memberInfo).GetValue(entity);
            return Tuple.Create(propertyName, propertyValue);
        }

        private static Tuple<string, TProperty> GetPropertyTuple3<T, TProperty>(
            T entity, Expression<Func<T, TProperty>> expression)
        {
            var member = expression.GetMember();
            var propertyValue = AccessorCache<T>.GetCachedAccessor(member, expression)(entity);
            return Tuple.Create(member.Name, propertyValue);
        }

        private static Tuple<string, TProperty> GetPropertyTuple4<T, TProperty>(
            T entity, Func<T, TProperty> func)
        {
            return Tuple.Create(QuickName.GetProperyInfo(func).Name, func(entity));
        }

        private static Tuple<string, TProperty> GetPropertyTuple5<T, TProperty>(
            T entity, Func<T, IPropertyMetadata<T, TProperty>> metadataFunc)
        {
            var metadata = metadataFunc(default(T));
            return Tuple.Create(metadata.PropertyName, metadata.Func(entity));
        }
    }

    public class Person
    {
        public string FirstName { get; set; }
        public string SecondName { get; set; }
    }

    public static class PersonExtensions
    {
        private static readonly IPropertyMetadata<Person, string> firstName = default(Person).Metadata(_ => _.FirstName);
        public static IPropertyMetadata<Person, string> FirstName(this Person @this) => firstName;

        private static readonly IPropertyMetadata<Person, string> secondName = default(Person).Metadata(_ => _.FirstName);
        public static IPropertyMetadata<Person, string> SecondName(this Person @this) => secondName;
    }
}
