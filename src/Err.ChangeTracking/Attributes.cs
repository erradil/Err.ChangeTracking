using System;

namespace Err.ChangeTracking;

/// <summary>
///     Tracking mode that indicates whether all properties should be tracked or only those
///     explicitly marked with the [TrackOnly] attribute
/// </summary>
public enum TrackingMode
{
    /// <summary>
    ///     Track all eligible properties
    /// </summary>
    All = 0,

    /// <summary>
    ///     Only track properties marked with [TrackOnly] attribute
    /// </summary>
    OnlyMarked = 1
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