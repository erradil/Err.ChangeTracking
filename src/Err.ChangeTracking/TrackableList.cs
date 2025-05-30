using System;
using System.Collections.Generic;
using System.Linq;

namespace Err.ChangeTracking;

public class TrackableList<T> : List<T>, IChangeTrackerBase
    where T : class
{
    private bool _hasStructuralChanges;

    public TrackableList()
    {
    }

    public TrackableList(int capacity) : base(capacity)
    {
    }

    public TrackableList(IEnumerable<T>? items)
    {
        if (items == null) return;

        // Pre-allocate capacity if possible
        if (items is ICollection<T> collection)
            Capacity = collection.Count;

        foreach (var item in items)
            base.Add(item.AsTrackable());
    }

    public bool IsDirty(bool deepTracking = false)
    {
        return _hasStructuralChanges ||
               this.OfType<ITrackable<T>>().Any(x => x.GetChangeTracker().IsDirty(deepTracking));
    }

    #region Item Access and Assignment

    public new T this[int index]
    {
        get => base[index];
        set
        {
            _hasStructuralChanges = true;
            base[index] = value.AsTrackable();
        }
    }

    #endregion

    #region Add Methods

    public new void Add(T item)
    {
        _hasStructuralChanges = true;
        base.Add(item.AsTrackable());
    }

    public new void AddRange(IEnumerable<T> items)
    {
        _hasStructuralChanges = true;

        // Convert to array to avoid multiple enumeration and get count
        var itemsArray = items as T[] ?? items.ToArray();

        // Pre-allocate capacity if needed
        if (Capacity < Count + itemsArray.Length)
            Capacity = Count + itemsArray.Length;

        // Add items efficiently
        foreach (var item in itemsArray)
            base.Add(item.AsTrackable());
    }

    #endregion

    #region Insert Methods

    public new void Insert(int index, T item)
    {
        _hasStructuralChanges = true;
        base.Insert(index, item.AsTrackable());
    }

    public new void InsertRange(int index, IEnumerable<T> items)
    {
        _hasStructuralChanges = true;
        base.InsertRange(index, items.Select(item => item.AsTrackable()));
    }

    #endregion

    #region Remove Methods

    public new bool Remove(T item)
    {
        var result = base.Remove(item);
        if (result)
            _hasStructuralChanges = true;
        return result;
    }

    public new void RemoveAt(int index)
    {
        _hasStructuralChanges = true;
        base.RemoveAt(index);
    }

    public new int RemoveAll(Predicate<T> match)
    {
        var result = base.RemoveAll(match);
        if (result > 0)
            _hasStructuralChanges = true;
        return result;
    }

    public new void RemoveRange(int index, int count)
    {
        if (count > 0)
            _hasStructuralChanges = true;
        base.RemoveRange(index, count);
    }

    public new void Clear()
    {
        if (Count > 0)
            _hasStructuralChanges = true;
        base.Clear();
    }

    #endregion

    #region Sort and Reverse Methods

    public new void Reverse()
    {
        if (Count > 1)
            _hasStructuralChanges = true;
        base.Reverse();
    }

    public new void Reverse(int index, int count)
    {
        if (count > 1)
            _hasStructuralChanges = true;
        base.Reverse(index, count);
    }

    public new void Sort()
    {
        if (Count > 1)
            _hasStructuralChanges = true;
        base.Sort();
    }

    public new void Sort(Comparison<T> comparison)
    {
        if (Count > 1)
            _hasStructuralChanges = true;
        base.Sort(comparison);
    }

    public new void Sort(IComparer<T>? comparer)
    {
        if (Count > 1)
            _hasStructuralChanges = true;
        base.Sort(comparer);
    }

    public new void Sort(int index, int count, IComparer<T>? comparer)
    {
        if (count > 1)
            _hasStructuralChanges = true;
        base.Sort(index, count, comparer);
    }

    #endregion
}