using System;

namespace Err.ChangeTracking;

public enum TrackingMode
{
    All,
    OnlyMarked
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class TrackableAttribute : Attribute
{
    public TrackingMode Mode { get; set; } = TrackingMode.All;
}

[AttributeUsage(AttributeTargets.Property)]
public class NotTrackedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class TrackCollectionAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class TrackOnlyAttribute : Attribute
{
}