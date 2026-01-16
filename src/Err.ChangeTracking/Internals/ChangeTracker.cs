using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Err.ChangeTracking.Internals;

internal class ChangeTracker<TEntity>(TEntity entity) : IChangeTracker<TEntity>
{
    private readonly Dictionary<string, object?> _originalValues = [];
    public bool IsEnabled { get; private set; }
    public bool DeepTracking { get; private set; }

    public static IChangeTracker<TEntity> Create(TEntity entity)
    {
        return new ChangeTracker<TEntity>(entity);
    }
    
    public void UseDeepTracking()
    {
        if(DeepTracking) 
            return;
        
        DeepTracking = true;
    
        var propertiesGetters = DeepTracking<TEntity>.Properties;
        
        foreach (var getProperty in propertiesGetters)
        {
            var property = getProperty(entity);
            
            
            switch (property.PropertyValue)
            {
                case  IList list:
                    (list as IChangeTracker)?.UseDeepTracking();
                    foreach (var item in list)
                    {
                        // Only process items that implement ITrackable
                        if(item is not ITrackable)
                            break;
                        item.AsTrackable(deepTracking: DeepTracking);
                    }
                    break;
                case  IDictionary dictionary:
                    (dictionary as IChangeTracker)?.UseDeepTracking();
                    foreach(var item in dictionary.Values)
                    {
                        // Only process items that implement ITrackable
                        if(item is not ITrackable) 
                            break;
                        item.AsTrackable(deepTracking: DeepTracking);
                    }
                    break;
                default: 
                    property.PropertyValue?.AsTrackable(deepTracking: DeepTracking);
                    break;
            }
        }
    }
    
    public IChangeTracker<TEntity> Enable(bool enable = true)
    {
        IsEnabled = enable;
        return this;
    }

    public bool IsDirty(bool? deepTracking = null)
    {
        return _originalValues is { Count: > 0 }
               || ((deepTracking ?? DeepTracking) && DeepTracking<TEntity>.HasDeepChanges(entity));
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
        if (_originalValues.TryGetValue(propertyName, out var value)) 
            return (TProperty?)value;

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

    [Obsolete("Use RecordChange(currentValue, newValue) instead. The property name is now captured automatically via [CallerMemberName].")]
    public void RecordChange<TProperty>(string propertyName, TProperty? currentValue, TProperty? newValue)
        => RecordChange(currentValue, newValue,propertyName);
   
    public IChangeTracker<TEntity> RecordChange<TProperty>(TProperty? currentValue, TProperty? newValue,
        [CallerMemberName] string? propertyName = null)
    {
        if (!IsEnabled || EqualityComparer<TProperty?>.Default.Equals(newValue, currentValue))
            return this;

        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

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
        return this;
    }

    public  IChangeTracker<TEntity> Rollback()
    {
        if (!IsEnabled || entity == null || _originalValues is { Count: 0 })
            return this;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        foreach (var (propertyName, originalValue) in _originalValues)
            PropertyHelper<TEntity>.TrySetProperty(entity, propertyName, originalValue);

        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Clear();
        return this;
    }

    public  IChangeTracker<TEntity> Rollback(string propertyName)
    {
        if (!IsEnabled || entity == null || _originalValues is { Count: 0 })
            return this;

        if (!_originalValues.TryGetValue(propertyName, out var originalValue))
            return this;

        IsEnabled = false; // Disable tracking temporarily to avoid recording changes when restoring original value
        PropertyHelper<TEntity>.TrySetProperty(entity, propertyName, originalValue);
        IsEnabled = true; // Re-enable tracking after rollback

        _originalValues.Remove(propertyName);
        return this;
    }

    public  IChangeTracker<TEntity> Rollback<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        Rollback(propertyName);
        return this;
    }

    public  IChangeTracker<TEntity> AcceptChanges()
    {
        _originalValues.Clear();
        return this;
    }

    public  IChangeTracker<TEntity> AcceptChanges(string propertyName)
    {
        _originalValues.Remove(propertyName);
        return this;
    }

    public  IChangeTracker<TEntity> AcceptChanges<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = PropertyHelper<TEntity>.GetPropertyName(propertyExpression);
        AcceptChanges(propertyName);
        return this;
    }
}