#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public class Model : ITrackable<Model>
{
    static Model()
    {
        // we identify all trackable properties for deep tracking
        DeepChangeTracking<Model>.SetDeepTrackableProperties([
            x => x.SubModel?.GetChangeTracker(), // if property is Trackable<T>
            x => x.Items as IBaseTracking // if property Is ITrackableCollection
        ]);
    }

    private IChangeTracking<Model>? _changeTracker;

    public IChangeTracking<Model> GetChangeTracker()
    {
        return _changeTracker ??= ChangeTracking.Create(this);
    }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            _changeTracker?.RecordChange(nameof(Name), _name, value);
            _name = value;
        }
    }

    private TrackableList<SubModel>? _items;

    public List<SubModel>? Items
    {
        get => _items;
        set
        {
            _changeTracker?.RecordChange(nameof(Items), _items, value);
            _items = value is null ? null : new TrackableList<SubModel>(value);
        }
    }


    private SubModel _subModel;

    public SubModel SubModel
    {
        get => _subModel;
        set
        {
            _changeTracker?.RecordChange(nameof(SubModel), _subModel, value);
            _subModel = value;
        }
    }
}

[Trackable]
public partial class SubModel
{
    public partial string Name { get; set; }
}