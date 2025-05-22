#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Err.ChangeTracking.SampleDemo.Models;

public class Model : ITrackable<Model>
{
    static Model()
    {
        // we identify all trackable properties for deep tracking
        DeepTracking<Model>.SetTrackableProperties([
            x => x.SubModel?.GetChangeTracker(), // if property is Trackable<T>
            x => x.Items as IBaseTracker // if property Is ITrackableCollection
        ]);
    }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            this.GetChangeTracker().RecordChange(nameof(Name), _name, value);
            _name = value;
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


    private SubModel _subModel;

    public SubModel SubModel
    {
        get => _subModel;
        set
        {
            this.GetChangeTracker().RecordChange(nameof(SubModel), _subModel, value);
            _subModel = value;
        }
    }
}

[Trackable]
public partial class SubModel
{
    public partial string Name { get; set; }
}