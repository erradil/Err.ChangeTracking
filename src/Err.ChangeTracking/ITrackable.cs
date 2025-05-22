using System.Runtime.CompilerServices;
using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public interface ITrackable<T> where T : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracking<T> GetChangeTracker()
    {
        return TrackingCache<T>.GetOrCreate((T)this);
    }
}

/// <summary>
///     Cache to maintain one tracker per instance
/// </summary>
internal static class TrackingCache<T> where T : class
{
    private static readonly ConditionalWeakTable<T, IChangeTracking<T>> Cache = new();

    public static IChangeTracking<T> GetOrCreate(T instance)
    {
        return Cache.GetValue(instance, ChangeTracking<T>.Create);
    }
}