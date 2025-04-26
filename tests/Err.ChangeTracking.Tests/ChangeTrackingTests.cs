using Xunit;
using Err.ChangeTracking.Simple;

namespace Err.ChangeTracking.Tests;

public class ChangeTrackingTests
{
    [Fact]
    public void Person_Should_Track_Simple_Properties()
    {
        var person = new Person { FirstName = "Alice", Age = 30 }.AsTrackable();

        person.FirstName = "Bob";

        var tracker = person.GetChangeTracker();

        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(x => x.FirstName));
    }

    [Fact]
    public void Order_Should_Track_Collections()
    {
        var order = new Order { Items = [], Prices = [] }.AsTrackable();

        order.Items.AsTrackable().Add("Item1");

        var tracker = order.Items.AsTrackable();

        Assert.True(tracker.IsDirty);
    }

    [Fact]
    public void Invoice_Should_Respect_TrackingMode_OnlyMarked()
    {
        var invoice = new Invoice { InvoiceNumber = "INV001", Comment = "Test" }.AsTrackable();

        invoice.InvoiceNumber = "INV002";
        invoice.Comment = "New Comment";

        var tracker = invoice.GetChangeTracker();

        Assert.True(tracker.HasChanged(x => x.InvoiceNumber));
        Assert.False(tracker.HasChanged(x => x.Comment));
    }

    [Fact]
    public void Invoice_Should_NotTrack_NotTrackedProperties()
    {
        var invoice = new Invoice { CreatedDate = DateTime.UtcNow }.AsTrackable();

        invoice.CreatedDate = DateTime.UtcNow.AddDays(1);

        var tracker = invoice.GetChangeTracker();

        Assert.False(tracker.HasChanged(x => x.CreatedDate));
    }
}
