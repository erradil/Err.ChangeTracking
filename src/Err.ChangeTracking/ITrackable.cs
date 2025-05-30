namespace Err.ChangeTracking;

public interface ITrackableBase<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracker<TEntity> GetChangeTracker();
}

public interface ITrackable<TEntity> : ITrackableBase<TEntity> where TEntity : class
{
    IChangeTracker<TEntity> ITrackableBase<TEntity>.GetChangeTracker()
    {
        return ChangeTrackerFactory.GetOrCreate((TEntity)this);
    }
}