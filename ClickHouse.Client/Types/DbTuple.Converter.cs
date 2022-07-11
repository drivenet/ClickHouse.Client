using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ClickHouse.Client.Types
{
    public static partial class DbTuple
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;
        private static readonly Func<Type, Lazy<Func<object, IDbTuple>>> DbTupleFactory =
            type => new Lazy<Func<object, IDbTuple>>(() => CreateFromTuple(type));

        private static readonly ConcurrentDictionary<Type, Lazy<Func<object, IDbTuple>>> FactoryCache
            = new ConcurrentDictionary<Type, Lazy<Func<object, IDbTuple>>>();

        private static readonly ISet<Type> TupleTypes = new HashSet<Type>
        {
            typeof(Tuple<>),
            typeof(Tuple<,>),
            typeof(Tuple<,,>),
            typeof(Tuple<,,,>),
            typeof(Tuple<,,,,>),
            typeof(Tuple<,,,,,>),
            typeof(Tuple<,,,,,,>),
        };

        private static readonly ISet<Type> ValueTupleTypes = new HashSet<Type>
        {
            typeof(ValueTuple<>),
            typeof(ValueTuple<,>),
            typeof(ValueTuple<,,>),
            typeof(ValueTuple<,,,>),
            typeof(ValueTuple<,,,,>),
            typeof(ValueTuple<,,,,,>),
            typeof(ValueTuple<,,,,,,>),
        };

        internal static IDbTuple Convert(object value)
        {
            if (TryConvert(value, out var tuple))
            {
                return tuple;
            }

            throw new ArgumentException("The provided value could not be converted to IDbTuple.", nameof(value));
        }

        internal static bool TryConvert(object value, out IDbTuple tuple)
        {
            if (value == null)
            {
                tuple = null;
                return false;
            }

            if (value is IDbTuple dbTuple)
            {
                tuple = dbTuple;
                return true;
            }

            if (FindTupleType(value.GetType()) is { } tupleType)
            {
                var factory = FactoryCache.GetOrAdd(tupleType, DbTupleFactory).Value;
                tuple = factory(value);
                return true;
            }

            tuple = null;
            return false;
        }

        internal static Type GetDbTupleType(int length) => length switch
        {
            1 => typeof(DbTuple<>),
            2 => typeof(DbTuple<,>),
            3 => typeof(DbTuple<,,>),
            4 => typeof(DbTuple<,,,>),
            5 => typeof(DbTuple<,,,,>),
            6 => typeof(DbTuple<,,,,,>),
            7 => typeof(DbTuple<,,,,,,>),
            _ => throw new NotSupportedException(),
        };

        private static Type FindTupleType(Type type)
        {
            if (type.IsValueType)
            {
                return type.IsConstructedGenericType && ValueTupleTypes.Contains(type.GetGenericTypeDefinition()) ? type : null;
            }

            while (type != null)
            {
                if (type.IsConstructedGenericType && TupleTypes.Contains(type.GetGenericTypeDefinition()))
                {
                    return type;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Func<object, IDbTuple> CreateFromTuple(Type tupleType)
        {
            var isValueTuple = tupleType.IsValueType;
            var typeArguments = tupleType.GetGenericArguments();
            var length = typeArguments.Length;

            var targetType = GetDbTupleType(length).MakeGenericType(typeArguments);

            var source = Expression.Parameter(typeof(object), "source");
            var sourceTuple = Expression.Convert(source, tupleType);

            var args = new Expression[length];
            for (var i = 0; i < length; ++i)
            {
                var memberName = $"Item{i + 1}";
                var member = isValueTuple
                    ? (MemberInfo)tupleType.GetField(memberName, MemberFlags)
                    : tupleType.GetProperty(memberName, MemberFlags);

                var nthItem = Expression.MakeMemberAccess(sourceTuple, member);
                args[i] = nthItem;
            }

            var target = Expression.New(targetType.GetConstructor(MemberFlags, null, typeArguments, null), args);
            var tupleValue = Expression.Convert(target, typeof(IDbTuple));

            var lambda = Expression.Lambda<Func<object, IDbTuple>>(tupleValue, typeof(Func<object, IDbTuple>).ToString(), new[] { source });
            return lambda.Compile();
        }
    }
}
