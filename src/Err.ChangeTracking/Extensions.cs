using System;
using System.Collections.Generic;

namespace Err.ChangeTracking;

public static class Extensions
{
    public static IChangeTracker<T> GetChangeTracker<T>(this T model)
        where T : class, ITrackable<T>
    {
        return model.GetChangeTracker();
    }

    public static T AsTrackable<T>(this T model)
        where T : class
    {
        if (model is ITrackable<T> trackable)
            trackable.GetChangeTracker().Enable();

        return model;
    }

    public static TrackableDictionary<TKey, TValue> AsTrackable<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        where TValue : class where TKey : notnull
    {
        if (dictionary is TrackableDictionary<TKey, TValue> trackable)
            return trackable;

        throw new ArgumentException("the dictionary is not TrackableDictionary<TKey,TValue>.", nameof(dictionary));
    }

    public static TrackableList<T> AsTrackable<T>(this List<T> collection)
        where T : class
    {
        if (collection is TrackableList<T> trackable)
            return trackable;

        throw new ArgumentException("the collection is not TrackableList<T>.", nameof(collection));
    }
}