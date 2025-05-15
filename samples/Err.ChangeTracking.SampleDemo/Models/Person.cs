namespace Err.ChangeTracking.SampleDemo.Models;

[Trackable(Mode = TrackingMode.OnlyMarked)]
internal partial record Employee
{
    [TrackOnly] public partial string Name { get; set; }
    [TrackOnly] protected partial int? Age { get; set; }

    [TrackCollection] [TrackOnly] public partial List<string>? Items { get; set; }

    [TrackOnly] public partial List<string> Managers { get; init; }

    [Trackable]
    public partial struct Address
    {
        public partial string City { get; set; }
        public partial string Zipcode { get; set; }
    }
}

[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial record Order
{
    public List<string>? Numbers { get; set; }
    [TrackOnly] public partial string Title { get; set; }
}