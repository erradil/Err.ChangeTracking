namespace Err.ChangeTracking.SampleDemo.Models;

[Trackable(Mode = TrackingMode.All)]
internal partial record Person
{
    public partial string Name { get; set; }

    [Trackable]
    public partial struct Address
    {
        public partial string Street { get; set; }
    }
}