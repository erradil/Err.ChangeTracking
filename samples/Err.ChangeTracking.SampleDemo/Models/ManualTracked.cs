#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public class Model : ITrackable<Model>, IAttachedTracker<Model>
{
    // Attaches inline change tracker to instance (direct field access, faster than cache-based tracking (default))
    IChangeTracker<Model>? IAttachedTracker<Model>.ChangeTracker { get; set; }
    
    static Model() // optional, but recommended to set up deep tracking properties once at startup
    {
        // we identify all trackable properties for deep tracking
        DeepTracking<Model>.SetTrackableProperties([
            x => x.SubModel?.TryGetChangeTracker(),    // if property is Trackable<T> entity
            x => x.Items?.TryGetChangeTracker()        // if property Is Trackable Collection
        ]);
    }
    
    public required string Name { get; set => this.SetField(ref field!, value); }
    
    public List<SubModel>? Items { get; set => this.SetField(ref field, value); }

    public SubModel? SubModel { get; set => this.SetField(ref field, value); }
}

[Trackable]
public partial class SubModel ()
{
    public partial string Name { get; set; }
}