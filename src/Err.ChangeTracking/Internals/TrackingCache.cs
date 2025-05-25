using System.Runtime.CompilerServices;

namespace Err.ChangeTracking.Internals;

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