namespace Err.ChangeTracking.SampleDemo.Models;

// Case 1: Class with default tracking mode (All properties tracked)
public partial record Order
{
    // case 3: Property already implemented, so we explicitly excluded from tracking 
    private DateTime _createdDate;

    public partial DateTime CreatedDate
    {
        get => _createdDate;
        set => _createdDate = value;
    }
}

[Trackable]
public partial record Order
{
    // Case 2: Simple property tracked by default (TrackingMode.All)
    public partial string Id { get; set; }

    // Case 3: Property already implemented, so we explicitly excluded from tracking 
    [NotTracked] public partial DateTime CreatedDate { get; set; }

    // Case 4: Tracked collection of simple types
    [TrackCollection] public partial List<string> Tags { get; set; }

    // Case 5: Tracked collection of OrderItem objects
    [TrackCollection] public partial List<OrderItem> Items { get; set; }

    // Case 6: Non-partial property (won't be tracked)
    public string Notes { get; set; }
}

// Case 7: Another trackable class for order items
[Trackable]
public partial class OrderItem
{
    // Case 8: Simple tracked properties
    public partial int Quantity { get; set; }
    public partial decimal UnitPrice { get; set; }

    // Case 9: Reference to another trackable object
    public partial Product Product { get; set; }
}

// Case 10: Class with selective tracking mode
[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial record Product
{
    // Case 11: Property explicitly marked for tracking
    [TrackOnly] public partial string Description { get; set; }

    // Case 12: Tracked collection with TrackOnly
    [TrackOnly] [TrackCollection] public partial List<string> Categories { get; set; }
}