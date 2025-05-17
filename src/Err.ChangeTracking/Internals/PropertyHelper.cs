using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking.Internals;

internal static class PropertyHelper
{
    internal static string GetPropertyName<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        return propertyExpression.Body switch
        {
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            MemberExpression directMember => directMember.Member.Name,
            _ => throw new ArgumentException("Invalid expression. Expected property access.")
        };
    }

    internal static Dictionary<string, Action<TEntity, object?>> BuildPropertySetters<TEntity>()
    {
        var setters = new Dictionary<string, Action<TEntity, object?>>();
        foreach (var prop in typeof(TEntity).GetProperties())
        {
            if (!prop.CanWrite)
                continue;

            var instanceParam = Expression.Parameter(typeof(TEntity));
            var valueParam = Expression.Parameter(typeof(object));

            var body = Expression.Assign(
                Expression.Property(instanceParam, prop),
                Expression.Convert(valueParam, prop.PropertyType)
            );

            var lambda = Expression.Lambda<Action<TEntity, object?>>(body, instanceParam, valueParam);
            setters[prop.Name] = lambda.Compile();
        }

        return setters;
    }
}