using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Err.ChangeTracking;

public class TrackableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IBaseTracker,
    IEnumerable<KeyValuePair<TKey, TValue>>
    where TValue : class
{
    private bool _hasStructuralChanges;

    public TrackableDictionary()
    {
    }

    public TrackableDictionary(IDictionary<TKey, TValue> dictionary)
    {
        // Manually add each item and apply wrapping to all initial values
        if (dictionary != null)
            foreach (var pair in dictionary)
                base.Add(pair.Key, pair.Value.AsTrackable());
    }

    // Add a constructor that accepts an IEnumerable of KeyValuePairs
    public TrackableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        // Manually add each item and apply wrapping to all initial values
        if (collection != null)
            foreach (var pair in collection)
                base.Add(pair.Key, pair.Value.AsTrackable());
    }


    public bool IsDirty(bool deepTracking = false)
    {
        return _hasStructuralChanges ||
               Values.OfType<ITrackable<TValue>>().Any(x => x.GetChangeTracker().IsDirty(deepTracking));
    }

    public new TValue this[TKey key]
    {
        get => base[key].AsTrackable();
        set
        {
            _hasStructuralChanges = true;
            base[key] = value.AsTrackable();
        }
    }

    public new bool TryGetValue(TKey key, out TValue value)
    {
        var result = base.TryGetValue(key, out value);
        value.AsTrackable();
        return result;
    }

    public new bool TryAdd(TKey key, TValue value)
    {
        _hasStructuralChanges = true;
        return base.TryAdd(key, value.AsTrackable());
    }

    public new void Add(TKey key, TValue value)
    {
        _hasStructuralChanges = true;
        base.Add(key, value.AsTrackable());
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

    // Override GetEnumerator to wrap each KeyValuePair value with AsTrackable
    public new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var pair in (Dictionary<TKey, TValue>)this)
            yield return new KeyValuePair<TKey, TValue>(pair.Key, pair.Value.AsTrackable());
    }

    // Implement the non-generic IEnumerable.GetEnumerator
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Override the Keys and Values properties to ensure values are trackable
    public new KeyCollection Keys => base.Keys;

    public new IEnumerable<TValue> Values
    {
        get
        {
            foreach (var key in Keys) yield return this[key]; // This will use our trackable indexer
        }
    }
}