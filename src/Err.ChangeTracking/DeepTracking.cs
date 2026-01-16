using System;
using System.Collections.Generic;

namespace Err.ChangeTracking;

public static class DeepTracking<TEntity>
{
    private static readonly object _objLock = new();
    private static readonly List<Func<TEntity, (object? PropertyValue, IChangeTracker? ChangeTracker)>> _properties = [];
    public static IReadOnlyList<Func<TEntity, (object? PropertyValue, IChangeTracker? ChangeTracker)>> Properties => _properties;

    private static void AddProperty<TProperty>(Func<TEntity, TProperty?> propertyGetter)
    where  TProperty : class
    {
        lock (_objLock)
        {
            _properties.Add(entity =>
            {
                var propertyValue = propertyGetter(entity);
                var changeTracker = propertyValue switch
                {
                    ITrackable<TProperty> trackable => trackable.TryGetChangeTracker(),
                    IChangeTracker collection => collection,
                    _ => null
                };
                return (propertyValue, changeTracker);
            });
        }
    }

    public static void Track<TProperty>(Func<TEntity, ITrackable<TProperty>?> propertyGetter)
        where TProperty : class
        => AddProperty(propertyGetter);

    public static void Track<TItem>(Func<TEntity, List<TItem>?> propertyGetter)
        where TItem : class, ITrackable<TItem>
        => AddProperty(propertyGetter);

    public static void Track<TKey, TValue>(Func<TEntity, Dictionary<TKey, TValue>?> propertyGetter)
        where TValue : class, ITrackable<TValue>
        where TKey : notnull
        => AddProperty(propertyGetter);
    
    
    public static bool HasDeepChanges(TEntity entity)
    {
        if (_properties is null or [])
            return false;

        foreach (var getProperty in _properties)
        {
            var property = getProperty(entity);
            if (property.ChangeTracker?.IsDirty(deepTracking: true) is true)
                return true;
        }

        return false;
    }
}