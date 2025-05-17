using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Err.ChangeTracking.Internals;

namespace Err.ChangeTracking;

public static class ChangeTracking
{
    public static IChangeTracking<TEntity> Create<TEntity>(TEntity entity)
    {
        return new ChangeTracking<TEntity>(entity);
    }
}

public interface IChangeTracking<TEntity>
{
    bool IsEnabled { get; }
    bool IsDirty { get; }

    public IChangeTracking<TEntity> Enable(bool enable = true);

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