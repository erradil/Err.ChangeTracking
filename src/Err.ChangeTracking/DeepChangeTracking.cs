using System;
using System.Collections.Generic;

namespace Err.ChangeTracking;

public static class DeepChangeTracking<T>
{
    private static readonly object _objLock = new();
    private static List<Func<T, IBaseTracking?>>? _deepTrackableProperties;

    public static void SetDeepTrackableProperties(List<Func<T, IBaseTracking?>> value)
    {
        lock (_objLock)
            _deepTrackableProperties ??= value;
    }

    public static bool HasDeepChanges(T entity)
    {
        if (_deepTrackableProperties is null or [])
            return false;

        foreach (var getProperty in _deepTrackableProperties)
        {
            var value = getProperty(entity);
            if (value?.IsDirty(true) is true)
                return true;
        }

        return false;
    }
}