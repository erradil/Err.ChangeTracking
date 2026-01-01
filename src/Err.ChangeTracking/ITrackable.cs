using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public interface IAttachedTracker<TEntity> where TEntity : class
{
    IChangeTracker<TEntity>? ChangeTracker { get; set; }
}

public interface ITrackable<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracker<TEntity>? TryGetChangeTracker()
        =>  (this is IAttachedTracker<TEntity> tracker)
            ? tracker.ChangeTracker
            :TrackingCache<TEntity>.TryGet((TEntity)this);
    
    /// <summary>
    ///     Gets Or Create the change tracker for this entity
    /// </summary>
    IChangeTracker<TEntity> GetOrCreateChangeTracker() 
        => (this is IAttachedTracker<TEntity> tracker)
            ? tracker.ChangeTracker ??= ChangeTracker<TEntity>.Create((TEntity)this)
            : TrackingCache<TEntity>.GetOrCreate((TEntity)this);
}