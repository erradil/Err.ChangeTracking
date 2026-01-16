namespace Err.ChangeTracking;

public interface ITrackable { }
public interface ITrackable<TEntity> : ITrackable where TEntity : class { }
public interface ITrackableCollection : ITrackable, IChangeTracker { }

public interface IAttachedTracker<TEntity> where TEntity : class
{
    IChangeTracker<TEntity>? ChangeTracker { get; set; }
}