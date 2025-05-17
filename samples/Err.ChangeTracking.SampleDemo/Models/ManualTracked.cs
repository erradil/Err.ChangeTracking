#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public partial class Model
{
    public partial string Name { get; set; }

    public partial List<string>? Items { get; set; }
}

public partial class Model : ITrackable<Model>
{
    private IChangeTracking<Model>? _changeTracker;

    public IChangeTracking<Model> GetChangeTracker()
    {
        return _changeTracker ??= ChangeTracking.Create(this);
    }

    private string _name;

    public partial string Name
    {
        get => _name;
        set
        {
            _changeTracker?.RecordChange(nameof(Name), _name, value);
            _name = value;
        }
    }

    private TrackableList<string>? _items;

    public partial List<string>? Items
    {
        get => _items;
        set
        {
            _changeTracker?.RecordChange(nameof(Items), _items, value);
            _items = value is null ? null : new TrackableList<string>(value);
        }
    }
}