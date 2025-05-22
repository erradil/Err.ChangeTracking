using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking.Internals;

internal class ChangeTracker<TEntity>(TEntity instance) : IChangeTracker<TEntity>
{
    private readonly Dictionary<string, object?> _originalValues = [];
    public bool IsEnabled { get; private set; }

    public static IChangeTracker<TEntity> Create(TEntity entity)
    {
        return new ChangeTracker<TEntity>(entity);
    }

    public IChangeTracker<TEntity> Enable(bool enable = true)
    {
        IsEnabled = enable;
        return this;
    }

    public bool IsDirty(bool deepTracking = false)
    {
        return _originalValues is { Count: > 0 }
               || (deepTracking && DeepTracking<TEntity>.HasDeepChanges(instance));
    }

    public IReadOnlyDictionary<string, object?> GetOriginalValues()
    {
        return _originalValues;
    }

    public TProperty? GetOriginalValue<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        return GetOriginalValue<TProperty>(propertyName);
    }

    public TProperty? GetOriginalValue<TProperty>(string propertyName)
    {
        if (_originalValues.TryGetValue(propertyName, out var value) is true) return (TProperty?)value;

        return default;
    }

    public IReadOnlyCollection<string> GetChangedProperties()
    {
        return _originalValues.Keys;
    }

    public bool HasChanged(string propertyName)
    {
        return _originalValues.ContainsKey(propertyName);
    }

    public bool HasChanged<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        return HasChanged(propertyName);
    }


    public void RecordChange<TProperty>(string propertyName, TProperty? currentValue, TProperty? newValue)
    {
        if (!IsEnabled || EqualityComparer<TProperty?>.Default.Equals(newValue, currentValue))
            return;


        if (_originalValues.TryGetValue(propertyName, out var originalValue))
        {
            // Rollback change tracking when the property returns to its original value,
            // effectively canceling out the modification as if it never happened.
            if (EqualityComparer<TProperty?>.Default.Equals(newValue, (TProperty?)originalValue))
                Rollback(propertyName);
        }
        else
        {
            _originalValues.Add(propertyName, currentValue);
        }
    }

    public void Rollback()
    {
        if (!IsEnabled || instance == null || _originalValues is { Count: 0 })
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        foreach (var (propertyName, originalValue) in _originalValues)
            PropertyHelper<TEntity>.TrySetProperty(instance, propertyName, originalValue);

        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Clear();
    }

    public void Rollback(string propertyName)
    {
        if (!IsEnabled || instance == null || _originalValues is { Count: 0 })
            return;

        if (!_originalValues.TryGetValue(propertyName, out var originalValue))
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        PropertyHelper<TEntity>.TrySetProperty(instance, propertyName, originalValue);
        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Remove(propertyName);
    }

    public void Rollback<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        Rollback(propertyName);
    }

    public void AcceptChanges()
    {
        _originalValues.Clear();
    }

    public void AcceptChanges(string propertyName)
    {
        _originalValues.Remove(propertyName);
    }

    public void AcceptChanges<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        AcceptChanges(propertyName);
    }
}