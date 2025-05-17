using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking.Internals;

internal class ChangeTracking<TEntity>(TEntity instance) : IChangeTracking<TEntity>
{
    private static readonly Lazy<Dictionary<string, Action<TEntity, object?>>> _propertiesSettersImpl =
        new(PropertyHelper.BuildPropertySetters<TEntity>);

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

    public TProperty? GetOriginalValue<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper.GetPropertyName(propertyExpression);
        return GetOriginalValue<TProperty>(propertyName);
    }

    public TProperty? GetOriginalValue<TProperty>(string propertyName)
    {
        if (_originalValues?.TryGetValue(propertyName, out var value) is true) return (TProperty?)value;

        return default;
    }

    public IReadOnlyCollection<string> GetChangedProperties()
    {
        return _originalValues?.Keys ?? (IReadOnlyCollection<string>)_emptyChanges.Keys;
    }

    public bool HasChanged(string propertyName)
    {
        return _originalValues?.ContainsKey(propertyName) ?? false;
    }

    public bool HasChanged<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper.GetPropertyName(propertyExpression);
        return HasChanged(propertyName);
    }


    public void RecordChange<TProperty>(string propertyName, TProperty? currentValue, TProperty? newValue)
    {
        if (!IsEnabled && EqualityComparer<TProperty?>.Default.Equals(currentValue, newValue))
            return;

        _originalValues ??= [];

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
            if (_propertiesSettersImpl.Value.TryGetValue(keyValue.Key, out var setter))
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
            !_propertiesSettersImpl.Value.TryGetValue(propertyName, out var setter))
            return;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        setter(instance, originalValue);
        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Remove(propertyName);

        if (_originalValues.Count == 0)
            IsDirty = false;
    }

    public void Rollback<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper.GetPropertyName(propertyExpression);
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

    public void AcceptChanges<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper.GetPropertyName(propertyExpression);
        AcceptChanges(propertyName);
    }
}