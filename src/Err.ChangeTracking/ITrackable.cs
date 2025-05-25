namespace Err.ChangeTracking;

public interface ITrackable<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracker<TEntity> GetChangeTracker()
    {
        return ChangeTrackerFactory.GetOrCreate((TEntity)this, true);
    }
}

public interface ITrackableBase<TEntity> where TEntity : class
{
    /// <summary>
    ///     Gets the change tracker for this entity
    /// </summary>
    IChangeTracker<TEntity> GetChangeTracker();
}