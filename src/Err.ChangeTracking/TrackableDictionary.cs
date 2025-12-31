using System.Collections.Generic;
using System.Linq;

namespace Err.ChangeTracking;

public class TrackableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IChangeTracker
    where TValue : class
    where TKey : notnull
{
    private bool _hasStructuralChanges;

    public TrackableDictionary()
    {
    }

    public TrackableDictionary(int capacity) : base(capacity)
    {
    }

    public TrackableDictionary(IEqualityComparer<TKey>? comparer) : base(comparer)
    {
    }

    public TrackableDictionary(int capacity, IEqualityComparer<TKey>? comparer) : base(capacity, comparer)
    {
    }

    public TrackableDictionary(IDictionary<TKey, TValue>? dictionary)
    {
        if (dictionary == null) return;

        // Pre-allocate capacity if possible
        if (dictionary.Count > 0)
            EnsureCapacity(dictionary.Count);

        foreach (var pair in dictionary)
            base.Add(pair.Key, pair.Value.AsTrackable());
    }

    public TrackableDictionary(IDictionary<TKey, TValue>? dictionary, IEqualityComparer<TKey>? comparer) :
        base(comparer)
    {
        if (dictionary == null) return;

        // Pre-allocate capacity if possible
        if (dictionary.Count > 0)
            EnsureCapacity(dictionary.Count);

        foreach (var pair in dictionary)
            base.Add(pair.Key, pair.Value.AsTrackable());
    }

    public TrackableDictionary(IEnumerable<KeyValuePair<TKey, TValue>>? collection)
    {
        if (collection == null) return;

        // Pre-allocate capacity if possible
        if (collection is ICollection<KeyValuePair<TKey, TValue>> coll && coll.Count > 0)
            EnsureCapacity(coll.Count);

        foreach (var pair in collection)
            base.Add(pair.Key, pair.Value.AsTrackable());
    }

    public TrackableDictionary(IEnumerable<KeyValuePair<TKey, TValue>>? collection, IEqualityComparer<TKey>? comparer) :
        base(comparer)
    {
        if (collection == null) return;

        // Pre-allocate capacity if possible
        if (collection is ICollection<KeyValuePair<TKey, TValue>> coll && coll.Count > 0)
            EnsureCapacity(coll.Count);

        foreach (var pair in collection)
            base.Add(pair.Key, pair.Value.AsTrackable());
    }

    public bool IsDirty(bool deepTracking = false)
    {
        return _hasStructuralChanges ||
               Values.OfType<ITrackable<TValue>>().Any(x => x.TryGetChangeTracker()?.IsDirty(deepTracking) ?? false);
    }

    #region Item Access and Assignment

    public new TValue this[TKey key]
    {
        get => base[key];
        set
        {
            _hasStructuralChanges = true;
            base[key] = value.AsTrackable();
        }
    }

    #endregion

    #region Access Methods

    public new bool TryGetValue(TKey key, out TValue value)
    {
        return base.TryGetValue(key, out value);
    }

    #endregion

    #region Add Methods

    public new void Add(TKey key, TValue value)
    {
        _hasStructuralChanges = true;
        base.Add(key, value.AsTrackable());
    }

    public new bool TryAdd(TKey key, TValue value)
    {
        var result = base.TryAdd(key, value.AsTrackable());
        if (result)
            _hasStructuralChanges = true;
        return result;
    }

    #endregion

    #region Remove Methods

    public new bool Remove(TKey key)
    {
        var result = base.Remove(key);
        if (result)
            _hasStructuralChanges = true;
        return result;
    }

    public new bool Remove(TKey key, out TValue value)
    {
        var result = base.Remove(key, out value);
        if (result)
            _hasStructuralChanges = true;
        return result;
    }

    public new void Clear()
    {
        if (Count > 0)
            _hasStructuralChanges = true;
        base.Clear();
    }

    #endregion

    #region Collection Properties

    public new KeyCollection Keys => base.Keys;

    public new ValueCollection Values => base.Values;

    #endregion
}