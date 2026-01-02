namespace Err.ChangeTracking;

public interface IAttachedTracker<TEntity> where TEntity : class
{
    IChangeTracker<TEntity>? ChangeTracker { get; set; }
}

public interface ITrackable<TEntity> where TEntity : class { }