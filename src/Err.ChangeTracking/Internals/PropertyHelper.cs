using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking.Internals;

internal static class PropertyHelper<TEntity>
{
    /// <summary>
    ///     Lazily initialized dictionary of compiled property setters for performance.
    /// </summary>
    private static readonly Lazy<Dictionary<string, Action<TEntity, object?>>> PropertiesSettersImpl =
        new(BuildPropertySetters);

    /// <summary>
    ///     Builds compiled expression-based property setters for all writable properties.
    ///     This avoids repeated reflection overhead when setting property values.
    ///     Much faster than using reflection for each property update.
    /// </summary>
    private static Dictionary<string, Action<TEntity, object?>> BuildPropertySetters()
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

    /// <summary>
    ///     Extracts property name from lambda expression.
    ///     Handles both direct access (x => x.Name) and boxed value types (x => x.Age where Age is int).
    /// </summary>
    internal static string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        return propertyExpression.Body switch
        {
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            MemberExpression directMember => directMember.Member.Name,
            _ => throw new ArgumentException("Invalid expression. Expected property access.")
        };
    }

    /// <summary>
    ///     Sets a property value using cached setters for performance. Returns success status.
    /// </summary>
    internal static bool TrySetProperty<TProperty>(TEntity instance, string propertyName, TProperty value)
    {
        if (PropertiesSettersImpl.Value.TryGetValue(propertyName, out var setter))
        {
            setter(instance, value);
            return true;
        }

        return false;
    }
}