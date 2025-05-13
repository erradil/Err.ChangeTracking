using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking;

public class ChangeTracking<TEntity>(TEntity instance) : IChangeTracking<TEntity>
{
    private static readonly Lazy<Dictionary<string, Action<TEntity, object?>>> PropertySetters =
        new(BuildPropertySetters);

    private static readonly IReadOnlyDictionary<string, object?> _emptyChanges = new Dictionary<string, object?>();

    private Dictionary<string, object?>? _originalValues;

    public bool IsDirty { get; private set; }
    public bool IsEnabled { get; private set; }

    public IChangeTracking<TEntity> Enable(bool enable = true)
    {
        IsEnabled = enable;
        return this;
    }

    public IReadOnlyDictionary<string, object?> GetOriginalValues()
    {
        return _originalValues ?? _emptyChanges;
    }

    public bool HasChanged(string propertyName)
    {
        return _originalValues?.ContainsKey(propertyName) ?? false;
    }

    public bool HasChanged(Expression<Func<TEntity, object?>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);
        return HasChanged(propertyName);
    }

    public void RecordChange<TProperty>(string propertyName, TProperty? currentValue, TProperty? newValue)
    {
        if (!IsEnabled && EqualityComparer<TProperty?>.Default.Equals(currentValue, newValue))
            return;

        _originalValues ??= [];

        //_originalValues.TryAdd(propertyName, currentValue);
        if (_originalValues.TryGetValue(propertyName, out var originalValue))
        {
            if (EqualityComparer<TProperty?>.Default.Equals(currentValue, (TProperty?)originalValue))
                Rollback(propertyName);
        }
        else
        {
            _originalValues.Add(propertyName, currentValue);
            IsDirty = true;
        }
    }

    public void Rollback()
    {
        if (!IsEnabled || instance == null || _originalValues == null)
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value

        foreach (var keyValue in _originalValues)
            if (PropertySetters.Value.TryGetValue(keyValue.Key, out var setter))
                setter(instance, keyValue.Value);

        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Clear();
        IsDirty = false;
    }

    public void Rollback(string propertyName)
    {
        if (!IsEnabled || instance == null || _originalValues == null)
            return;

        if (!_originalValues.TryGetValue(propertyName, out var originalValue) ||
            !PropertySetters.Value.TryGetValue(propertyName, out var setter))
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        setter(instance, originalValue);
        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Remove(propertyName);

        if (_originalValues.Count == 0)
            IsDirty = false;
    }

    public void Rollback(Expression<Func<TEntity, object?>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);
        Rollback(propertyName);
    }

    public void AcceptChanges()
    {
        _originalValues?.Clear();
        IsDirty = false;
    }

    public void AcceptChanges(string propertyName)
    {
        _originalValues?.Remove(propertyName);
        if (_originalValues is { Count: 0 })
            IsDirty = false;
    }

    public void AcceptChanges(Expression<Func<TEntity, object?>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);
        AcceptChanges(propertyName);
    }

    private static string GetPropertyName<T>(Expression<Func<T, object?>> propertyExpression)
    {
        return propertyExpression.Body switch
        {
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            MemberExpression directMember => directMember.Member.Name,
            _ => throw new ArgumentException("Invalid expression. Expected property access.")
        };
    }

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
}