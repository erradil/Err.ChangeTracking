using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Err.ChangeTracking;

public interface IChangeTracker
{
    bool IsDirty(bool? deepTracking = null);
    bool DeepTracking { get; }
    void UseDeepTracking();
}

public interface IChangeTracker<TEntity> : IChangeTracker
{
    bool IsEnabled { get; }
    
    public IChangeTracker<TEntity> Enable(bool enable = true);
    

    IReadOnlyCollection<string> GetChangedProperties();
    
    bool HasChanged(string propertyName);
    
    bool HasChanged<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);

    IReadOnlyDictionary<string, object?> GetOriginalValues();
    
    public TProperty? GetOriginalValue<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);
    
    public TProperty? GetOriginalValue<TProperty>(string propertyName);

    IChangeTracker<TEntity> RecordChange<TProperty>(TProperty? currentValue, TProperty? newValue,
        [CallerMemberName] string? propertyName = null);

    [Obsolete("Use RecordChange(currentValue, newValue) instead. The property name is now captured automatically via [CallerMemberName].")]
    void RecordChange<TProperty>(string propertyName, TProperty currentValue, TProperty newValue);

    IChangeTracker<TEntity> Rollback();
    
    IChangeTracker<TEntity> Rollback(string propertyName);
    
    IChangeTracker<TEntity> Rollback<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);

    IChangeTracker<TEntity> AcceptChanges();
    
    IChangeTracker<TEntity> AcceptChanges(string propertyName);
    
    IChangeTracker<TEntity> AcceptChanges<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);
    
}