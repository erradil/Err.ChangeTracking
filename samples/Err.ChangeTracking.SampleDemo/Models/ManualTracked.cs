#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public class Model2 : ITrackableBase<Model2>
{
    // Set Change Tracker to a private field, rather than get it from cache each time (could add some micro optimization)
    private IChangeTracker<Model2>? _changeTracker;

    public IChangeTracker<Model2> GetChangeTracker()
    {
        return _changeTracker ??= ChangeTrackerFactory.GetOrCreate(this);
    }

    public required string Name
    {
        get;
        set
        {
            _changeTracker?.RecordChange(field, value);
            field = value;
        }
    }
}

public class Model : ITrackable<Model>
{
    static Model() // optional, but recommended to set up deep tracking properties once at startup
    {
        // we identify all trackable properties for deep tracking
        DeepTracking<Model>.SetTrackableProperties([
            x => x.SubModel?.GetChangeTracker(), // if property is Trackable<T>
            x => x.Items as IChangeTrackerBase // if property Is ITrackableCollection
        ]);
    }

    public required string Name
    {
        get;
        set
        {
            this.GetChangeTracker().RecordChange(field, value);
            field = value;
        }
    }

    private TrackableList<SubModel>? _items;

    public List<SubModel>? Items
    {
        get => _items;
        set
        {
            this.GetChangeTracker().RecordChange(nameof(Items), _items, value);
            _items = value is null ? null : new TrackableList<SubModel>(value);
        }
    }


    public required SubModel SubModel
    {
        get;
        set
        {
            this.GetChangeTracker().RecordChange(nameof(SubModel), field, value);
            field = value;
        }
    }
}

[Trackable]
public partial class SubModel
{
    public partial string Name { get; set; }
}