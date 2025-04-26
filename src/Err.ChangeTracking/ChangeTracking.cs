using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Err.ChangeTracking;

public class ChangeTracking<TEntity> : IChangeTracking<TEntity>
{
    private static readonly Lazy<Dictionary<string, Action<TEntity, object?>>> _propertySetters = new(() => BuildPropertySetters());

    private static IReadOnlyDictionary<string, object?> _emptyChanges = ImmutableDictionary<string, object?>.Empty;

    private Dictionary<string, object?>? _originalValues;
    private readonly TEntity _instance;

    public ChangeTracking(TEntity instance)
    {
        _instance = instance;
    }

    public bool IsDirty { get; private set; } = false;
    public bool IsEnabled { get; private set; }

    public IChangeTracking<TEntity> Enable(bool enable = true)
    {
        IsEnabled = enable;
        return this;
    }

    public IReadOnlyDictionary<string, object?> GetOriginalValues() => _originalValues ?? _emptyChanges;

    public bool HasChanged(string propertyName) => _originalValues?.ContainsKey(propertyName) ?? false;

    public bool HasChanged(Expression<Func<TEntity, object?>> propertyExpression)
    {
        string propertyName = GetPropertyName(propertyExpression);
        return HasChanged(propertyName);
    }

    public void RecordChange<TProperty>(string propertyName, TProperty originalValue, TProperty newValue)
    {
        if (!IsEnabled || EqualityComparer<TProperty>.Default.Equals(originalValue, newValue))
            return;

        _originalValues ??= [];

        //_originalValues.TryAdd(propertyName, originalValue);
        if(!_originalValues.ContainsKey(propertyName))
            _originalValues.Add(propertyName, originalValue);

        IsDirty = true;
    }

    public void Rollback()
    {
        if (!IsEnabled || _instance == null || _originalValues == null)
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value

        foreach (var keyValue in _originalValues)
        {
            if (_propertySetters.Value.TryGetValue(keyValue.Key, out var setter))
            {
                setter(_instance, keyValue.Value);
            }
        }
        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Clear();
        IsDirty = false;
    }

    public void Rollback(string propertyName)
    {
        if (!IsEnabled || _instance == null || _originalValues == null)
            return;

        if (_originalValues.TryGetValue(propertyName, out var originalValue) &&
            _propertySetters.Value.TryGetValue(propertyName, out var setter))
        {
            IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
            setter(_instance, originalValue);
            IsEnabled = true; // Re-enable tracking after rollback

            _originalValues.Remove(propertyName);

            if (_originalValues.Count == 0)
                IsDirty = false;
        }
    }

    public void Rollback(Expression<Func<TEntity, object?>> propertyExpression)
    {
        string propertyName = GetPropertyName(propertyExpression);
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
        string propertyName = GetPropertyName(propertyExpression);
        AcceptChanges(propertyName);
    }

    private static string GetPropertyName<T>(Expression<Func<T, object?>> propertyExpression)
    {
        if (propertyExpression.Body is UnaryExpression unary && unary.Operand is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (propertyExpression.Body is MemberExpression directMember)
        {
            return directMember.Member.Name;
        }

        throw new ArgumentException("Invalid expression. Expected property access.");
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