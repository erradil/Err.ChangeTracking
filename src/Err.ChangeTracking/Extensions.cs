using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Err.ChangeTracking;

public static class Extensions
{
    public static IChangeTracker<T>? TryGetChangeTracker<T>(this ITrackable<T> model)
        where T : class, ITrackable<T>
        => model.TryGetChangeTracker();
    
    public static IChangeTracker<T> GetChangeTracker<T>(this ITrackable<T> model)
        where T : class, ITrackable<T>
        => model.TryGetChangeTracker() 
           ?? throw new InvalidOperationException("ChangeTracker is not initialized!. Use AsTrackable() first. Or use TryGetChangeTracker() instead.");
    
    public static IChangeTracker? TryGetChangeTracker<T>(this List<T> model) 
        where T : class 
        => model as TrackableList<T> ;
    
    public static IChangeTracker? TryGetChangeTracker<TKey,TValue>(this Dictionary<TKey,TValue> model) 
        where TValue : class
        where TKey : notnull
        => model as TrackableDictionary<TKey, TValue> ;


    public static T AsTrackable<T>(this T model)
        where T : class
    {
        if (model is ITrackable<T> trackable)
            trackable.GetOrCreateChangeTracker()?.Enable();

        return model;
    }

    public static TrackableDictionary<TKey, TValue> AsTrackable<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        where TValue : class where TKey : notnull
    {
        if (dictionary is TrackableDictionary<TKey, TValue> trackable)
            return trackable;

        throw new ArgumentException("The dictionary is not TrackableDictionary<TKey,TValue>.", nameof(dictionary));
    }

    public static TrackableList<T> AsTrackable<T>(this List<T> collection)
        where T : class
    {
        if (collection is TrackableList<T> trackable)
            return trackable;

        throw new ArgumentException("The collection is not TrackableList<T>.", nameof(collection));
    }
    
    public static void SetField<TEntity, TField>(this ITrackable<TEntity> trackable, ref TField? field, TField value, [CallerMemberName] string? propertyName = null)
        where TEntity : class
    {
        trackable.TryGetChangeTracker()?.RecordChange(field, value,  propertyName);
        field = value;
    }
    
    public static void SetField<TEntity, TValue>(this ITrackable<TEntity> trackable, ref TrackableList<TValue>? field, List< TValue>? value, [CallerMemberName] string? propertyName = null)
        where TEntity : class
        where TValue : class
    {
        trackable.SetField(ref field, value is null ? null : new TrackableList<TValue>(value),  propertyName);
    }
    
    public static void SetField<TEntity, TKey, TValue>(this ITrackable<TEntity> trackable, ref TrackableDictionary<TKey, TValue>? field, Dictionary<TKey, TValue>?value, [CallerMemberName] string? propertyName = null)
        where TEntity : class
        where TValue : class
        where TKey : notnull
    {
        trackable.SetField(ref field, value is null ? null : new TrackableDictionary<TKey, TValue>(value),  propertyName);
    }
}