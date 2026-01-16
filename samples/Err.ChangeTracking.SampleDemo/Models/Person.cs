namespace Err.ChangeTracking.SampleDemo.Models;

[Trackable(Mode = TrackingMode.All)]
public partial record Person
{
    static Person()
    {
        DeepTracking<Person>.Track(x => x.Addr);
        //DeepTracking<Person>.Track(x => x.ListAddresses);
        DeepTracking<Person>.Track(x => x.Managers);
    }
    
    public partial string Name { get; set; }

    public TrackableDictionary<string, object>? Tags { get; set; }

    public TrackableList<string>? Options { get; set; }
    
    //[DeepTracking] 
    public partial Address? Addr { get; set; }
    [TrackCollection]
    public partial List<Address>? ListAddresses { get; set; }
    [TrackCollection]
    public partial List<Person>? Managers { get; set; }
    public partial Person? Manager { get; set; }

    [Trackable]
    public partial record Address
    {
        public partial string Street { get; set; }
        public string City => "Paris";
    }
}