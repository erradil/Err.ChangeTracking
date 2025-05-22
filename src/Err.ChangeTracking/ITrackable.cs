using System.Runtime.CompilerServices;
using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public interface ITrackable<T> where T : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracker<T> GetChangeTracker()
    {
        return TrackingCache<T>.GetOrCreate((T)this);
    }
}

/// <summary>
///     Cache to maintain one tracker per instance
/// </summary>
internal static class TrackingCache<T> where T : class
{
    private static readonly ConditionalWeakTable<T, IChangeTracker<T>> Cache = new();

    public static IChangeTracker<T> GetOrCreate(T instance)
    {
        return Cache.GetValue(instance, ChangeTracker<T>.Create);
    }
}