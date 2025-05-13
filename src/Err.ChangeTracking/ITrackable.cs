namespace Err.ChangeTracking;

public interface ITrackable<T>
{
    IChangeTracking<T> GetChangeTracker();
}