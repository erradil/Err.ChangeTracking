using System.Collections.Generic;
using System.Linq;

namespace Err.ChangeTracking;

public class TrackableList<T> : List<T>, ITrackableCollection
{
    private bool _hasStructuralChanges;

    public TrackableList()
    {
    }

    public TrackableList(IEnumerable<T> items) : base(items)
    {
    }

    public bool IsDirty =>
        _hasStructuralChanges ||
        this.OfType<ITrackable<T>>().Any(x => x.GetChangeTracker().IsDirty);

    public new T this[int index]
    {
        get => base[index].AsTrackable();
        set
        {
            _hasStructuralChanges = true;
            base[index] = Wrap(value);
        }
    }

    public new void Add(T item)
    {
        _hasStructuralChanges = true;
        base.Add(Wrap(item));
    }

    public new void AddRange(IEnumerable<T> items)
    {
        _hasStructuralChanges = true;
        base.AddRange(items.Select(Wrap));
    }

    public new bool Remove(T item)
    {
        _hasStructuralChanges = true;
        return base.Remove(item);
    }

    public new void RemoveAt(int index)
    {
        _hasStructuralChanges = true;
        base.RemoveAt(index);
    }

    public new void Clear()
    {
        _hasStructuralChanges = true;
        base.Clear();
    }

    private static T Wrap(T item)
    {
        return item.AsTrackable(); // Auto-enable item tracking on add
    }
}