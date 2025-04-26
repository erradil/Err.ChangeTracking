using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Err.ChangeTracking;

public interface IChangeTracking<T>
{
    bool IsEnabled { get; }
    bool IsDirty { get; }

    public IChangeTracking<T> Enable(bool enable = true);

    bool HasChanged(string propertyName);

    bool HasChanged(Expression<Func<T, object?>> propertyExpression);

    IReadOnlyDictionary<string, object?> GetOriginalValues();

    void RecordChange<TProperty>(string propertyName, TProperty currentValue, TProperty newValue);

    void Rollback();

    void Rollback(string propertyName);

    void Rollback(Expression<Func<T, object?>> propertyExpression);

    void AcceptChanges();

    void AcceptChanges(string propertyName);

    void AcceptChanges(Expression<Func<T, object?>> propertyExpression);
}