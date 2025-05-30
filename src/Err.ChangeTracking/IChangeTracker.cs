using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking;

public interface IChangeTrackerBase
{
    bool IsDirty(bool deepTracking = false);
}

public interface IChangeTracker<TEntity> : IChangeTrackerBase
{
    bool IsEnabled { get; }

    public IChangeTracker<TEntity> Enable(bool enable = true);

    IReadOnlyCollection<string> GetChangedProperties();
    bool HasChanged(string propertyName);
    bool HasChanged<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);

    IReadOnlyDictionary<string, object?> GetOriginalValues();
    public TProperty? GetOriginalValue<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);
    public TProperty? GetOriginalValue<TProperty>(string propertyName);

    void RecordChange<TProperty>(string propertyName, TProperty currentValue, TProperty newValue);

    void Rollback();
    void Rollback(string propertyName);
    void Rollback<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);

    void AcceptChanges();
    void AcceptChanges(string propertyName);
    void AcceptChanges<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);
}