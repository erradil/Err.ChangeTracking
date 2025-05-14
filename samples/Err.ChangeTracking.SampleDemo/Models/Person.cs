namespace Err.ChangeTracking.SampleDemo;

[Trackable(Mode = TrackingMode.OnlyMarked)]
internal partial record Employee
{
    [TrackOnly] public partial string Name { get; set; }
    [TrackOnly] protected static partial int? Age { get; set; }

    [TrackCollection] [TrackOnly] public partial List<string>? Items { get; set; }
    //public partial System.Collections.Generic.List<string> Managers { get; set; }

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
    [TrackOnly] public partial List<string>? numbers { get; set; }
    [TrackCollection] public partial List<string>? Prices { get; set; }
}