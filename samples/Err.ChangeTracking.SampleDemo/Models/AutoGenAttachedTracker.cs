namespace Err.ChangeTracking.SampleDemo.Models;

// Test class for AttachedTrackerGenerator
// by adding "partial keyword" the generator will generate the attached tracker for this class
// with inline change tracker property, faster than default cache-based tracking
public partial class TestModel : ITrackable<TestModel>
{
    public string? Name { get; set => this.SetField(ref field, value);}
}

// Another test with a nested class
public partial class OuterClass
{
    public partial class InnerModel : ITrackable<InnerModel>
    {
        public int Value { get; set => this.SetField(ref field, value);}
    }
}
