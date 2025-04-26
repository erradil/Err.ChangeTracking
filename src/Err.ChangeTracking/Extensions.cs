using System.Collections.Generic;

namespace Err.ChangeTracking;

public static class Extensions
{
    public static T AsTrackable<T>(this T model)
    {
        if (model is ITrackable<T> trackable)
        {
            trackable.GetChangeTracker().Enable();
        }
        return model;
    }

    public static TrackableDictionary<TKEY, TVALUE> AsTrackable<TKEY, TVALUE>(this Dictionary<TKEY, TVALUE> dictionary)
    {
        if (dictionary is TrackableDictionary<TKEY, TVALUE> trackable)
        {
            return trackable;
        }
        throw new System.Exception("the dictionary is not TrackableDictionary<TKEY,TVALUE>.");
    }

    public static TrackableList<T> AsTrackable<T>(this List<T> collection)
    {
        if (collection is TrackableList<T> trackable)
        {
            return trackable;
        }
        throw new System.Exception("the collection is not TrackableList<T>.");
    }
}