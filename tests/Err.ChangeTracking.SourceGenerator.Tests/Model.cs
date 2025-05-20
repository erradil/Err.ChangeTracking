using Err.ChangeTracking;
namespace Err.ChangeTracking.SourceGenerator.Tests;
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }
    
    [Trackable]
    public partial struct Address
    {
        public partial string Street { get; set; }
    }
}