using Err.ChangeTracking;
namespace Err.ChangeTracking.SourceGenerator.Tests;
internal partial record Person : Err.ChangeTracking.ITrackable<Person>
{
    // ITrackable interface implementation
    private Err.ChangeTracking.IChangeTracking<Person>? _changeTracker;
    public Err.ChangeTracking.IChangeTracking<Person> GetChangeTracker() => _changeTracker ??= ChangeTracking.Create(this);
        
    // This property is tracked by default based on TrackingMode.All
    private string _name;
    public partial string Name
    {
        get => _name;
        set { _changeTracker?.RecordChange(nameof(Name), _name, value); _name = value; }
    }

}