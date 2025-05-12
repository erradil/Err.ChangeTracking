namespace Err.ChangeTracking.SampleDemo;

[Trackable(Mode = TrackingMode.OnlyMarked)]
internal partial record Employee
{
    [TrackOnly] public partial string Name { get; set; }
    [TrackOnly] protected static partial int? Age { get; set; }

    [TrackCollection] public partial List<string>? Items { get; set; }

    [Trackable]
    public partial struct Address
    {
        public partial string City { get; set; }
        public partial string Zipcode { get; set; }
    }
}