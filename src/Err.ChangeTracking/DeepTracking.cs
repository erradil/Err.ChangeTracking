using System;
using System.Collections.Generic;

namespace Err.ChangeTracking;

public static class DeepTracking<T>
{
    private static readonly object _objLock = new();
    private static List<Func<T, IChangeTracker?>>? _deepTrackableProperties;

    public static void SetTrackableProperties(List<Func<T, IChangeTracker?>> value)
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
            if (value?.IsDirty(deepTracking: true) is true)
                return true;
        }

        return false;
    }
}