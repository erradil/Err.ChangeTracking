#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

/// <summary>
/// Demonstrates manual implementation of change tracking with all available features:
/// - ITrackable: Marks this class as trackable entity
/// - IAttachedTracker: Provides inline ChangeTracker property (faster than cache-based default)
/// - Deep tracking: Tracks changes in nested entities and collections
/// - C# 13 'field' keyword: Simplified property setter syntax with implicit backing field
/// </summary>
public class Model : ITrackable<Model>, IAttachedTracker<Model>
{
    // Explicit interface implementation: Attaches inline change tracker to instance
    // This is faster than cache-based tracking (default) because it stores the tracker
    // directly on the instance rather than looking it up in a ConditionalWeakTable
    IChangeTracker<Model>? IAttachedTracker<Model>.ChangeTracker { get; set; }
    
    /// <summary>
    /// Static constructor to configure deep tracking for nested trackable properties.
    /// This is optional but recommended for performance - it's executed once at startup
    /// instead of discovering trackable properties via reflection at runtime.
    /// </summary>
    static Model()
    {
        // Register all trackable properties for deep change tracking
        // Deep tracking automatically monitors changes in nested entities and collections
        // DeepTracking<Model>.SetTrackableProperties([
        //     x => x.SubModel?.TryGetChangeTracker(),    // Track changes in nested trackable entity
        //     x => x.Items?.TryGetChangeTracker()        // Track changes in trackable collection (add/remove/modify items)
        // ]);
        
        DeepTracking<Model>.Track(x => x.SubModel);
    }
    
    /// <summary>
    /// Simple property with change tracking using C# 13 'field' keyword.
    /// The 'field' keyword provides implicit access to the backing field.
    /// SetField records the change and updates the backing field.
    /// </summary>
    public required string Name { get; set => this.SetField(ref field!, value); }
    
    /// <summary>
    /// Trackable collection property. Changes to the list (add/remove items)
    /// and changes within items (if they implement ITrackable) are tracked.
    /// Use TrackableList&lt;T&gt; wrapper for automatic collection change tracking.
    /// </summary>
    public List<SubModel>? Items { get; set => this.SetField(ref field, value); }

    /// <summary>
    /// Nested trackable entity property. Changes to the SubModel's properties
    /// are tracked via deep tracking configuration above.
    /// </summary>
    public SubModel? SubModel { get; set => this.SetField(ref field, value); }
}

/// <summary>
/// SubModel uses cache-based tracking (default behavior).
/// Only implements ITrackable<SubModel> WITHOUT IAttachedTracker<SubModel>.
/// The ChangeTracker is stored in a ConditionalWeakTable cache instead of inline property.
/// This is simpler but slightly slower than attached tracker approach.
/// Use this when you don't need maximum performance or prefer automatic memory management.
/// </summary>
public class SubModel : ITrackable<SubModel>
{
    /// <summary>
    /// Simple property with change tracking using C# 13 'field' keyword.
    /// SetField records the change via cache-based tracker lookup.
    /// </summary>
    public string Name { get; set => this.SetField(ref field!, value); }
}