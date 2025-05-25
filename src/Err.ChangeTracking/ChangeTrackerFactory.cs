using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public static class ChangeTrackerFactory
{
    public static IChangeTracker<TEntity> GetOrCreate<TEntity>(TEntity entity, bool useCache = true)
        where TEntity : class
    {
        return useCache
            ? TrackingCache<TEntity>.GetOrCreate(entity)
            : ChangeTracker<TEntity>.Create(entity);
    }
}