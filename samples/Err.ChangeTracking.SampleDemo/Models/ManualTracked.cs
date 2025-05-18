using Err.ChangeTracking.Internals;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public partial class Model
{
    public partial string Name { get; set; }

    public partial SubModel SubModel { get; set; }
    public partial List<SubModel>? Items { get; set; }
    private TrackableList<SubModel>? _items;

    public partial List<SubModel>? Items
    {
        get => _items;
        set
        {
            _changeTracker?.RecordChange(nameof(Items), _items, value);
            _items = value is null ? null : new TrackableList<SubModel>(value);
        }
    }
}

[Trackable]
public partial class SubModel
{
    public partial string Name { get; set; }
}

public partial class SubModel
{
    static SubModel()
    {
        DeepChangeTracking<SubModel>.SetDeepTrackableProperties([]);
    }
}

//example of generated code
public partial class Model : ITrackable<Model>
{
    static Model()
    {
        // we identify all trackable properties for deep tracking
        DeepChangeTracking<Model>.SetDeepTrackableProperties([
            x => x.SubModel?.GetChangeTracker(), // if property is Trackable<T>
            x => x.Items as ITrackableCollection // if property Is ITrackableCollection
        ]);
    }

    private IChangeTracking<Model>? _changeTracker;

    public IChangeTracking<Model> GetChangeTracker()
    {
        if (_changeTracker is null) _changeTracker = ChangeTracking.Create(this);

        return _changeTracker;
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

    private SubModel _subModel;

    public partial SubModel SubModel
    {
        get => _subModel;
        set
        {
            _changeTracker?.RecordChange(nameof(SubModel), _subModel, value);
            _subModel = value;
        }
    }
}