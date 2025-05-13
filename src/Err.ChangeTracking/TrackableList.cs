using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Err.ChangeTracking;

public class TrackableList<T> : List<T>, ITrackableCollection, IEnumerable<T>

{
    private bool _hasStructuralChanges;

    public TrackableList()
    {
    }

    public TrackableList(IEnumerable<T> items)
    {
        if (items == null) return;
        foreach (var item in items) base.Add(item.AsTrackable());
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
            base[index] = value.AsTrackable();
        }
    }

    public new void Add(T item)
    {
        _hasStructuralChanges = true;
        base.Add(item.AsTrackable());
    }

    public new void AddRange(IEnumerable<T> items)
    {
        _hasStructuralChanges = true;
        base.AddRange(items.Select(i => i.AsTrackable()));
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

    // Override the GetEnumerator methods to wrap each item with AsTrackable
    public new IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return base[i].AsTrackable();
    }

    // Implement the non-generic IEnumerable.GetEnumerator
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}