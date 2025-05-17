using Err.ChangeTracking.SampleDemo.Models;

namespace Err.ChangeTracking.Tests;

public class OrderTrackingTests
{
    [Fact]
    public void Order_SimplePropertyTracking()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001"
        }.AsTrackable();

        // Act - Modify a simple tracked property
        order.Id = "ORD-002";

        // Assert
        var tracker = order.GetChangeTracker();
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.Equal("ORD-001", tracker.GetOriginalValues()[nameof(Order.Id)]);
        Assert.Equal("ORD-002", order.Id);
    }

    [Fact]
    public void Order_NotTrackedPropertyIgnored()
    {
        // Arrange
        var originalDate = DateTime.Today;
        var order = new Order
        {
            Id = "ORD-001",
            CreatedDate = originalDate
        }.AsTrackable();

        // Act - Modify a property marked with NotTracked
        var newDate = DateTime.Today.AddDays(1);
        order.CreatedDate = newDate;

        // Assert - Should not be tracked
        var tracker = order.GetChangeTracker();
        Assert.False(tracker.IsDirty);
        Assert.False(tracker.HasChanged(nameof(Order.CreatedDate)));
        // NotTracked properties won't be in the original values dictionary
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.CreatedDate)));
        Assert.Equal(newDate, order.CreatedDate);
    }

    [Fact]
    public void Order_NonPartialPropertyIgnored()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001",
            Notes = "Initial notes"
        }.AsTrackable();

        // Act - Modify a non-partial property
        order.Notes = "Modified notes";

        // Assert - Should not be tracked
        var tracker = order.GetChangeTracker();
        Assert.False(tracker.IsDirty);
        // Non-partial properties won't be in the original values dictionary
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.Notes)));
        Assert.Equal("Modified notes", order.Notes);
    }

    [Fact]
    public void Order_TagsCollectionTracking()
    {
        // Arrange
        var initialTags = new List<string> { "Important", "Priority" };
        var order = new Order
        {
            Id = "ORD-001",
            Tags = new List<string>(initialTags)
        }.AsTrackable();

        // Act - Modify tags collection
        order.Tags.AsTrackable().Add("Urgent");

        // Assert - Collection should be dirty
        Assert.True(order.Tags.AsTrackable().IsDirty);
        Assert.False(order.GetChangeTracker().IsDirty); // Model doesn't track collection changes directly
        Assert.Equal(3, order.Tags.Count);
        Assert.Contains("Urgent", order.Tags);
    }

    [Fact]
    public void Order_ReplacingTagsCollectionTracked()
    {
        // Arrange
        var initialTags = new List<string> { "Important" };
        var order = new Order
        {
            Id = "ORD-001",
            Tags = new List<string>(initialTags)
        }.AsTrackable();

        // Act - Replace entire collection
        var newTags = new List<string> { "New", "Tags" };
        order.Tags = newTags;

        // Assert - Model should detect collection replacement
        var tracker = order.GetChangeTracker();
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Order.Tags)));
        var originalTags = tracker.GetOriginalValues()[nameof(Order.Tags)] as List<string>;
        Assert.NotNull(originalTags);
        Assert.Single(originalTags);
        Assert.Equal("Important", originalTags[0]);
    }

    [Fact]
    public void Order_ItemsCollectionTracking()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { Quantity = 1, UnitPrice = 10.5m }
            }
        }.AsTrackable();

        // Act - Add item to collection
        order.Items.AsTrackable().Add(new OrderItem { Quantity = 2, UnitPrice = 20m });

        // Assert
        Assert.True(order.Items.AsTrackable().IsDirty);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Order_ModifyingNestedOrderItemTracked()
    {
        // Arrange
        var initialQuantity = 1;
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { Quantity = initialQuantity, UnitPrice = 10.5m }
            }
        }.AsTrackable();

        // Act - Modify property of nested item
        order.Items[0].Quantity = 5;

        // Assert
        var itemTracker = order.Items[0].GetChangeTracker();
        Assert.True(itemTracker.IsDirty);
        Assert.True(itemTracker.HasChanged(nameof(OrderItem.Quantity)));
        Assert.Equal(initialQuantity, itemTracker.GetOriginalValue(i => i.Quantity));
        Assert.Equal(5, order.Items[0].Quantity);
    }

    [Fact]
    public void Product_OnlyMarkedPropertiesTracked()
    {
        // Arrange - Product uses TrackingMode.OnlyMarked
        var initialDescription = "Original Description";
        var product = new Product
        {
            Description = initialDescription // This has TrackOnly attribute
        }.AsTrackable();

        // Act - Modify tracked property
        product.Description = "Updated Description";

        // Assert
        var tracker = product.GetChangeTracker();
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Product.Description)));
        Assert.Equal(initialDescription, tracker.GetOriginalValues()[nameof(Product.Description)]);
        Assert.Equal("Updated Description", product.Description);
    }

    [Fact]
    public void OrderItem_NestedProductTrackingWorks()
    {
        // Arrange
        var initialDescription = "Original";
        var orderItem = new OrderItem
        {
            Quantity = 1,
            UnitPrice = 10.5m,
            Product = new Product { Description = initialDescription }.AsTrackable()
        }.AsTrackable();

        // Act - Modify nested product
        orderItem.Product.Description = "Modified";

        // Assert
        var productTracker = orderItem.Product.GetChangeTracker();
        Assert.True(productTracker.IsDirty);
        Assert.True(productTracker.HasChanged(nameof(Product.Description)));
        Assert.Equal(initialDescription, productTracker.GetOriginalValues()[nameof(Product.Description)]);
        Assert.Equal("Modified", orderItem.Product.Description);
    }

    [Fact]
    public void Product_CategoriesCollection_WithTrackOnly()
    {
        // Arrange
        var product = new Product
        {
            Description = "Test Product",
            Categories = new List<string> { "Category1" }
        }.AsTrackable();

        // Act - Modify tracked collection
        product.Categories.AsTrackable().Add("Category2");

        // Assert
        Assert.True(product.Categories.AsTrackable().IsDirty);
        Assert.Equal(2, product.Categories.Count);
        Assert.Contains("Category2", product.Categories);
    }

    [Fact]
    public void Order_Rollback_RestoresSimpleProperties()
    {
        // Arrange
        var initialId = "ORD-001";
        var order = new Order
        {
            Id = initialId
        }.AsTrackable();

        // Act - Modify a simple property
        order.Id = "ORD-002";

        // Verify the property was changed
        var tracker = order.GetChangeTracker();
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.Equal(initialId, tracker.GetOriginalValues()[nameof(Order.Id)]);

        // Rollback the change
        order.GetChangeTracker().Rollback();

        // Assert - Property should be restored to its original value
        Assert.Equal(initialId, order.Id);
        Assert.False(tracker.IsDirty);
        Assert.False(tracker.HasChanged(nameof(Order.Id)));
    }


    [Fact]
    public void Order_AcceptChanges_ClearsDirtyFlags()
    {
        // Arrange
        var initialId = "ORD-001";
        var order = new Order
        {
            Id = initialId
        }.AsTrackable();

        // Act - Modify a simple property
        const string newId = "ORD-002";
        order.Id = newId;

        // Verify the property was changed
        var tracker = order.GetChangeTracker();
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.Equal(initialId, tracker.GetOriginalValues()[nameof(Order.Id)]);

        // Accept the change
        order.GetChangeTracker().AcceptChanges();

        // Assert - Dirty flags should be cleared while values remain changed
        Assert.Equal(newId, order.Id);
        Assert.False(tracker.IsDirty);
        Assert.False(tracker.HasChanged(nameof(Order.Id)));

        // Make another change to verify the original value was updated
        var newerId = "ORD-003";
        order.Id = newerId;

        // After accepting changes, the original value should be the accepted value
        Assert.True(tracker.IsDirty);
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.Equal(newId, tracker.GetOriginalValues()[nameof(Order.Id)]);
    }
}