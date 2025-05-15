using System;

namespace Err.ChangeTracking;

/// <summary>
///     Tracking mode that indicates whether all properties should be tracked or only those
///     explicitly marked with the [TrackOnly] attribute
/// </summary>
public enum TrackingMode
{
    /// <summary>
    ///     Track all eligible properties. When this mode is set, all partial properties
    ///     in a trackable class will be tracked for changes unless explicitly marked with
    ///     [NotTracked].
    /// </summary>
    All = 0,

    /// <summary>
    ///     Only track properties marked with [TrackOnly] attribute. When this mode is set,
    ///     only properties explicitly marked with [TrackOnly] will be tracked for changes.
    ///     Other properties will be ignored by the change tracking system.
    /// </summary>
    OnlyMarked = 1
}

/// <summary>
///     Marks a class or struct as trackable, enabling change tracking for its properties.
///     This attribute triggers the source generator to implement ITrackable and add change
///     tracking logic to partial properties.
/// </summary>
/// <remarks>
///     Classes marked with this attribute must:
///     1. Be declared as partial
///     2. Have partial properties that should be tracked
///     The source generator will automatically implement:
///     - Backing fields for properties
///     - Change tracking in property setters
///     - The ITrackable interface
/// </remarks>
/// <example>
///     <code>
///     [Trackable]
///     public partial class Person
///     {
///         public partial string FirstName { get; set; }
///         public partial int Age { get; set; }
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class TrackableAttribute : Attribute
{
    /// <summary>
    ///     Gets or sets the tracking mode for properties in this class.
    ///     Default is TrackingMode.All which tracks all eligible properties.
    /// </summary>
    /// <example>
    ///     <code>
    ///     [Trackable(Mode = TrackingMode.OnlyMarked)]
    ///     public partial class Document
    ///     {
    ///         [TrackOnly]
    ///         public partial string Title { get; set; }
    ///         
    ///         // This property won't be tracked since the mode is OnlyMarked
    ///         public partial string Description { get; set; }
    ///     }
    ///     </code>
    /// </example>
    public TrackingMode Mode { get; set; } = TrackingMode.All;
}

/// <summary>
///     Indicates that a property should NOT be tracked for changes, even if it would normally
///     be eligible for tracking. Use this to exclude specific properties from change tracking.
/// </summary>
/// <remarks>
///     This attribute takes precedence over other tracking configuration, including TrackingMode.All
///     or TrackOnly attributes. Properties marked with NotTracked will never be tracked.
/// </remarks>
/// <example>
///     <code>
///     [Trackable]
///     public partial class User
///     {
///         public partial string Username { get; set; }
///         
///         [NotTracked]
///         public partial DateTime LastLogin { get; set; } // Won't be tracked
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public class NotTrackedAttribute : Attribute
{
}

/// <summary>
///     Enables deep change tracking for collection properties (List&lt;T&gt; or Dictionary&lt;TKey,TValue&gt;).
///     When applied, the property will be wrapped in a trackable collection that monitors changes
///     to the collection itself (add/remove/clear operations).
/// </summary>
/// <remarks>
///     Currently supported collection types:
///     - System.Collections.Generic.List&lt;T&gt; → TrackableList&lt;T&gt;
///     - System.Collections.Generic.Dictionary&lt;TKey,TValue&gt; → TrackableDictionary&lt;TKey,TValue&gt;
///     The source generator will automatically wrap the original collection in the appropriate
///     trackable collection wrapper.
/// </remarks>
/// <example>
///     <code>
///     [Trackable]
///     public partial class Order
///     {
///         [TrackCollection]
///         public partial List&lt;string&gt; Items { get; set; }
///         
///         [TrackCollection]
///         public partial Dictionary&lt;string, decimal&gt; Prices { get; set; }
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public class TrackCollectionAttribute : Attribute
{
}

/// <summary>
///     Explicitly marks a property to be tracked for changes when the containing class uses
///     TrackingMode.OnlyMarked. This attribute has no effect when TrackingMode.All is used.
/// </summary>
/// <remarks>
///     Use this attribute when you want to selectively track only certain properties in a class
///     while ignoring the rest. It must be combined with [Trackable(Mode = TrackingMode.OnlyMarked)]
///     on the containing class.
/// </remarks>
/// <example>
///     <code>
///     [Trackable(Mode = TrackingMode.OnlyMarked)]
///     public partial class Invoice
///     {
///         [TrackOnly]
///         public partial string InvoiceNumber { get; set; } // Will be tracked
///         
///         [TrackOnly]
///         public partial decimal Amount { get; set; } // Will be tracked
///         
///         public partial string Comments { get; set; } // Won't be tracked
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public class TrackOnlyAttribute : Attribute
{
}