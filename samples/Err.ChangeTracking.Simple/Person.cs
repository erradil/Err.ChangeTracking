namespace Err.ChangeTracking.Simple;

[Trackable]
public partial class Person
{
    public partial string FirstName { get; set; }
    public partial int Age { get; set; }
}

[Trackable]
public partial class Order
{
    public partial List<string> Items { get; set; }

    public partial Dictionary<string, decimal> Prices { get; set; }

    [TrackCollection]
    public partial TrackableList<string> TrackableItems { get; set; }

    [TrackCollection]
    public partial TrackableDictionary<string, decimal> TrackablePrices { get; set; }
}




[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial class Invoice
{
    [TrackOnly]
    public partial string InvoiceNumber { get; set; }

    public string? Comment { get; set; }

    [NotTracked]
    public DateTime CreatedDate { get; set; }

    [TrackCollection, TrackOnly]
    public partial List<LineItem> Items { get; set; }
}


[Trackable]
public partial class LineItem
{
    public partial string ProductName { get; set; }
    public partial int Quantity { get; set; }
}