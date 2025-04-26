namespace Err.ChangeTracking;

[Trackable]
public partial record Person
{
    public partial string? FirstName { get; set; }
}

// auto generate by source code generator, when class is parial and has attribute Trackable
public partial record Person : ITrackable<Person>
{
    private IChangeTracking<Person>? _changeTracker;
    public IChangeTracking<Person> GetChangeTracker()
    {
        return _changeTracker ??= new ChangeTracking<Person>(this);
    }
    
    private string? _firstName;

    public partial string? FirstName
    {
        get => _firstName;
        set
        {
            _changeTracker?.RecordChange(nameof(FirstName), _firstName, value);
            _firstName = value;
        }
    }

    
}