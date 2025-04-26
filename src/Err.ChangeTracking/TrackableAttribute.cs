using System;

namespace Err.ChangeTracking;

public enum TrackingMode
{
    All,
    OnlyMarked
}

[AttributeUsage(AttributeTargets.Class)]
public class TrackableAttribute : Attribute
{
    public TrackingMode Mode { get; set; } = TrackingMode.All;
}

[AttributeUsage(AttributeTargets.Property)]
public class TrackOnlyAttribute : Attribute
{ }

[AttributeUsage(AttributeTargets.Property)]
public class NotTrackedAttribute : Attribute
{ }

/// <summary>
/// Attribut à placer sur les propriétés List<T> ou Dictionary<K,V>
/// pour activer le tracking automatique des éléments (ajouts, suppressions, et modifications internes).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TrackCollectionAttribute : Attribute
{
}