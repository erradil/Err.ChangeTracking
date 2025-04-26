using System.Linq;

namespace Err.ChangeTracking;

using System.Collections.Generic;

public class TrackableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ITrackableCollection
{
    private bool _hasStructuralChanges;

    public TrackableDictionary() : base()
    {
    }

    public TrackableDictionary(IDictionary<TKey, TValue> dictionary) : base(dictionary)
    {
    }

    public bool IsDirty =>
        _hasStructuralChanges ||
        Values.OfType<ITrackable<TValue>>().Any(x => x.GetChangeTracker().IsDirty);

    public new void Add(TKey key, TValue value)
    {
        _hasStructuralChanges = true;
        base.Add(key, Wrap(value));
    }

    public new bool Remove(TKey key)
    {
        _hasStructuralChanges = true;
        return base.Remove(key);
    }

    public new void Clear()
    {
        _hasStructuralChanges = true;
        base.Clear();
    }

    private static TValue Wrap(TValue item)
    {
        return item.AsTrackable(); // Auto enable item tracking.
    }
}