namespace Err.ChangeTracking.SampleDemo.Models;

[Trackable(Mode = TrackingMode.All)]
public partial record Person
{
    public partial string Name { get; set; }

    public TrackableDictionary<string, object>? Tags { get; set; }

    public TrackableList<string>? Options { get; set; }
    
    [DeepTracking] 
    public partial Address? Addr { get; set; }

    [Trackable]
    public partial record Address
    {
        public partial string Street { get; set; }
        public string City => "Paris";
    }
}