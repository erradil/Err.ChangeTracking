using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public static class Extensions
{
    extension<TEntity>(TEntity entity) where TEntity : class
    {
        public TEntity AsTrackable(bool deepTracking = false)
        {
            if (entity is ITrackable<TEntity> trackable)
                trackable.AsTrackable(deepTracking);

            return entity;
        }
    }
    
    extension<TEntity>(ITrackable<TEntity> trackable) 
        where TEntity : class
    {
        
        public TEntity AsTrackable(bool deepTracking = false)
        {
            var changeTracking  = trackable.GetOrCreateChangeTracker().Enable();
            
            if (!changeTracking.DeepTracking && deepTracking)
                changeTracking.UseDeepTracking();
            
            return (TEntity)trackable;
        }
        
        private IChangeTracker<TEntity> GetOrCreateChangeTracker() 
            => (trackable is IAttachedTracker<TEntity> tracker)
                ? tracker.ChangeTracker ??= ChangeTracker<TEntity>.Create((TEntity)trackable)
                : TrackingCache<TEntity>.GetOrCreate((TEntity)trackable);
        
        public IChangeTracker<TEntity> GetChangeTracker() 
            => trackable.TryGetChangeTracker() 
               ?? throw new InvalidOperationException("ChangeTracker is not initialized!. Use AsTrackable() first. Or use TryGetChangeTracker() instead.");

        public IChangeTracker<TEntity>? TryGetChangeTracker()
            =>(trackable is IAttachedTracker<TEntity> tracker)
                ? tracker.ChangeTracker
                :TrackingCache<TEntity>.TryGet((TEntity)trackable);
        
        // SetField overloads for primitive types
        public void SetField<TField, TValue>(ref TField field, TValue value, [CallerMemberName] string? propertyName = null)
            where TValue : struct, TField
        {
            trackable.TryGetChangeTracker()?.RecordChange(field, value ,  propertyName);
            field = value;
        }
        
        // SetField overloads for nullable primitive types
        public void SetField<TField>(ref TField? field, TField? value, [CallerMemberName] string? propertyName = null)
            where TField: struct
        {
            trackable.TryGetChangeTracker()?.RecordChange(field, value,  propertyName);
            field = value;
        }
        
        // SetField overloads for reference types
        public void SetField<TField>(ref TField? field, TField? value, [CallerMemberName] string? propertyName = null)
            where TField: class
        {
            var changeTracker = trackable.TryGetChangeTracker()?.RecordChange(field, value,  propertyName);
            field = value;

            if (changeTracker?.DeepTracking is true)
                field?.AsTrackable(deepTracking: true);
        }

        public void SetField<TValue>(ref TrackableList<TValue>? field, List< TValue>? value, [CallerMemberName] string? propertyName = null)
            where TValue : class
            => trackable.SetField(ref field, value is null ? null : new TrackableList<TValue>(value),  propertyName);
        
        public void SetField<TKey, TValue>(ref TrackableDictionary<TKey, TValue>? field, Dictionary<TKey, TValue>?value, [CallerMemberName] string? propertyName = null)
            where TValue : class
            where TKey : notnull
            => trackable.SetField(ref field, value is null ? null : new TrackableDictionary<TKey, TValue>(value),  propertyName);
    }

    extension<T>(List<T> list)
        where T : class
    {
        public IChangeTracker? TryGetChangeTracker() => list as IChangeTracker;

        public TrackableList<T> AsTrackable(bool deepTracking = false)
        {
            if(list is not TrackableList<T> trackableList)
                throw new ArgumentException("The collection is not TrackableList<T>.", nameof(list));

            if (!trackableList.DeepTracking && deepTracking)
                trackableList.UseDeepTracking();

            return trackableList;
        }
    }
    
    extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary) 
        where TValue : class 
        where TKey : notnull
    {
        public IChangeTracker? TryGetChangeTracker() => dictionary as IChangeTracker;

        public TrackableDictionary<TKey, TValue> AsTrackable(bool deepTracking = false)
        {
            if (dictionary is not TrackableDictionary<TKey, TValue> trackableDictionary)
                throw new ArgumentException("The collection is not TrackableDictionary<TKey,TValue>", nameof(dictionary));
            
            if (!trackableDictionary.DeepTracking && deepTracking)
                trackableDictionary.UseDeepTracking();

            return trackableDictionary;
        }
    }
}