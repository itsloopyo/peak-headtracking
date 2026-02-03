using System;
using System.Linq.Expressions;
using System.Reflection;

namespace PeakHeadTracking
{
    /// <summary>
    /// Shared utilities for compiled reflection delegates.
    /// Using compiled expression delegates is ~10-100x faster than FieldInfo.GetValue for hot path calls.
    /// </summary>
    public static class ReflectionUtils
    {
        /// <summary>
        /// Creates a compiled delegate for fast static field access.
        /// </summary>
        public static Func<TResult> CreateStaticFieldGetter<TResult>(FieldInfo field)
        {
            var fieldAccess = Expression.Field(null, field);
            var castResult = Expression.Convert(fieldAccess, typeof(TResult));
            return Expression.Lambda<Func<TResult>>(castResult).Compile();
        }

        /// <summary>
        /// Creates a compiled delegate for fast instance field access.
        /// </summary>
        public static Func<object, TResult> CreateInstanceFieldGetter<TResult>(Type instanceType, FieldInfo field)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(instanceParam, instanceType);
            var fieldAccess = Expression.Field(castInstance, field);
            var castResult = Expression.Convert(fieldAccess, typeof(TResult));
            return Expression.Lambda<Func<object, TResult>>(castResult, instanceParam).Compile();
        }

        /// <summary>
        /// Creates a compiled delegate for fast static property access.
        /// </summary>
        public static Func<TResult> CreateStaticPropertyGetter<TResult>(PropertyInfo property)
        {
            var propertyAccess = Expression.Property(null, property);
            var castResult = Expression.Convert(propertyAccess, typeof(TResult));
            return Expression.Lambda<Func<TResult>>(castResult).Compile();
        }
    }
}
