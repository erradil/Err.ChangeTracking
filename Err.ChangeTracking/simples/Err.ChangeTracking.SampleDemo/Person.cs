namespace Err.ChangeTracking.SampleDemo;

[Trackable]
internal partial record Employee
{
    public partial string? Name { get; set; }
    protected static partial string? Age { get; set; }
}