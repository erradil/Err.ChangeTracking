using Err.ChangeTracking.SampleDemo.Models;

namespace Err.ChangeTracking.Tests;

public class PropertyTrackingTests
{
    [Fact]
    public void Property_Tracking_Workflow()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001"
        }.AsTrackable();

        var tracker = order.GetChangeTracker();
        Assert.False(tracker.IsDirty());

        // Act 1 - Change simple property
        order.Id = "ORD-002";

        // Assert 1 - Verify tracking
        Assert.True(tracker.IsDirty());
        Assert.True(tracker.HasChanged(x => x.Id));
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.Equal("ORD-001", tracker.GetOriginalValue(x => x.Id));
        Assert.Equal("ORD-001", tracker.GetOriginalValues()[nameof(Order.Id)]);
        Assert.Equal("ORD-002", order.Id);

        // Act 2 - Change property again
        order.Id = "ORD-003";

        // Assert 2 - Original value remains the first value
        Assert.True(tracker.IsDirty());
        Assert.Equal("ORD-001", tracker.GetOriginalValue(x => x.Id));
        Assert.Equal("ORD-003", order.Id);

        // Act 3 - AcceptChanges
        tracker.AcceptChanges();

        // Assert 3 - Clean state, new baseline established
        Assert.False(tracker.IsDirty());
        Assert.False(tracker.HasChanged(x => x.Id));
        Assert.Equal("ORD-003", order.Id);

        // Act 4 - Make new change after accepting
        order.Id = "ORD-004";

        // Assert 4 - New original value is the accepted value
        Assert.True(tracker.IsDirty());
        Assert.Equal("ORD-003", tracker.GetOriginalValue(x => x.Id));
        Assert.Equal("ORD-004", order.Id);
    }

    [Fact]
    public void Property_NotTracked_Scenarios()
    {
        // Arrange
        var originalDate = DateTime.Today;
        var order = new Order
        {
            Id = "ORD-001",
            CreatedDate = originalDate,
            Notes = "Initial notes"
        }.AsTrackable();

        var tracker = order.GetChangeTracker();

        // Act 1 - Modify [NotTracked] property
        var newDate = DateTime.Today.AddDays(1);
        order.CreatedDate = newDate;

        // Assert 1 - NotTracked property not tracked
        Assert.False(tracker.IsDirty());
        Assert.False(tracker.HasChanged(nameof(Order.CreatedDate)));
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.CreatedDate)));
        Assert.Equal(newDate, order.CreatedDate);

        // Act 2 - Modify non-partial property
        order.Notes = "Modified notes";

        // Assert 2 - Non-partial property not tracked
        Assert.False(tracker.IsDirty());
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.Notes)));
        Assert.Equal("Modified notes", order.Notes);

        // Act 3 - Modify tracked property
        order.Id = "ORD-002";

        // Assert 3 - Only tracked property appears in original values
        Assert.True(tracker.IsDirty());
        Assert.Single(tracker.GetOriginalValues());
        Assert.Contains(nameof(Order.Id), tracker.GetOriginalValues().Keys);
    }
}

public class CollectionTrackingTests
{
    [Fact]
    public void Collection_List_CompleteWorkflow()
    {
        // Arrange
        var initialTags = new List<string> { "Important", "Priority" };
        var order = new Order
        {
            Id = "ORD-001",
            Tags = new List<string>(initialTags)
        }.AsTrackable();

        var orderTracker = order.GetChangeTracker();

        // Act 1 - Add item to collection (internal change)
        order.Tags.AsTrackable().Add("Urgent");

        // Assert 1 - Collection is dirty, but not the model (shallow tracking)
        Assert.True(order.Tags.AsTrackable().IsDirty());
        Assert.False(orderTracker.IsDirty());
        Assert.Equal(3, order.Tags.Count);

        // Act 2 - Remove item from collection
        order.Tags.AsTrackable().Remove("Priority");

        // Assert 2 - Collection still dirty
        Assert.True(order.Tags.AsTrackable().IsDirty());
        Assert.Equal(2, order.Tags.Count);
        Assert.DoesNotContain("Priority", order.Tags);

        // Act 3 - Replace entire collection (property change)
        var newTags = new List<string> { "New", "Tags" };
        order.Tags = newTags;

        // Assert 3 - Model detects collection replacement
        Assert.True(orderTracker.IsDirty());
        Assert.True(orderTracker.HasChanged(nameof(Order.Tags)));
        // Original value points to the first collection (which was modified internally)
        var originalTagsFromTracker = orderTracker.GetOriginalValues()[nameof(Order.Tags)] as List<string>;
        Assert.NotNull(originalTagsFromTracker);
        Assert.Equal(2, originalTagsFromTracker.Count);

        // Act 4 - Rollback collection replacement
        orderTracker.Rollback();

        // Assert 4 - Collection restored to original reference
        Assert.NotNull(order.Tags);
        Assert.Equal(2, order.Tags.Count);
        Assert.Contains("Important", order.Tags);
        // Note: "Priority" was removed earlier, so it won't be there
        Assert.Contains("Urgent", order.Tags);
        Assert.False(orderTracker.IsDirty());
    }

    [Fact]
    public void Collection_Dictionary_CompleteWorkflow()
    {
        // Arrange - Fresh dictionary each time to avoid state issues
        var order = new Order
        {
            Id = "ORD-001",
            Options = new Dictionary<string, string>
            {
                { "Color", "Blue" },
                { "Size", "Large" }
            }
        }.AsTrackable();

        var orderTracker = order.GetChangeTracker();

        // Act 1 - Add item to dictionary (internal change)
        order.Options.AsTrackable().Add("Priority", "High");

        // Assert 1 - Dictionary is dirty, model not dirty (shallow tracking)
        Assert.True(order.Options.AsTrackable().IsDirty());
        Assert.False(orderTracker.IsDirty());
        Assert.Equal(3, order.Options.Count);
        Assert.Equal("High", order.Options["Priority"]);

        // Act 2 - Update existing value via indexer (structural change)
        order.Options["Color"] = "Red";

        // Assert 2 - Dictionary still dirty
        Assert.True(order.Options.AsTrackable().IsDirty());
        Assert.Equal("Red", order.Options["Color"]);

        // Act 3 - Remove item
        order.Options.AsTrackable().Remove("Size");

        // Assert 3 - Dictionary still dirty, item removed
        Assert.True(order.Options.AsTrackable().IsDirty());
        Assert.Equal(2, order.Options.Count);
        Assert.False(order.Options.ContainsKey("Size"));

        // Act 4 - Replace entire dictionary (property change)
        var newOptions = new Dictionary<string, string>
        {
            { "Material", "Cotton" },
            { "Style", "Casual" }
        };
        order.Options = newOptions;

        // Assert 4 - Model detects dictionary replacement
        Assert.True(orderTracker.IsDirty());
        Assert.True(orderTracker.HasChanged(nameof(Order.Options)));

        // Act 5 - Rollback dictionary replacement
        orderTracker.Rollback();

        // Assert 5 - Dictionary restored to original reference (with modifications)
        Assert.NotNull(order.Options);
        Assert.Equal(2, order.Options.Count);
        Assert.Contains("Color", order.Options.Keys);
        Assert.Contains("Priority", order.Options.Keys);
        Assert.False(orderTracker.IsDirty());
    }
}

public class DeepTrackingTests
{
    [Fact]
    public void DeepTracking_NestedObjects_Workflow()
    {
        // Arrange - Create order with nested OrderItem
        var initialQuantity = 1;
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { Quantity = initialQuantity, UnitPrice = 10.5m }
            }
        }.AsTrackable();

        var orderTracker = order.GetChangeTracker();
        var item = order.Items[0];

        // Act 1 - Modify nested object property
        item.Quantity = 5;

        // Assert 1 - Nested object is dirty
        var itemTracker = item.GetChangeTracker();
        Assert.True(itemTracker.IsDirty());
        Assert.True(itemTracker.HasChanged(nameof(OrderItem.Quantity)));
        Assert.Equal(initialQuantity, itemTracker.GetOriginalValue(i => i.Quantity));
        Assert.Equal(5, item.Quantity);

        // Assert 2 - Parent not dirty (shallow tracking)
        Assert.False(orderTracker.IsDirty());

        // Act 2 - Add multiple nesting levels (Order -> OrderItem -> Product)
        var initialDescription = "Original Product";
        item.Product = new Product
        {
            Description = initialDescription
        }.AsTrackable();

        // Act 3 - Modify deeply nested property
        item.Product.Description = "Modified Product";

        // Assert 3 - Deeply nested tracking works
        var productTracker = item.Product.GetChangeTracker();
        Assert.True(productTracker.IsDirty());
        Assert.True(productTracker.HasChanged(nameof(Product.Description)));
        Assert.Equal(initialDescription, productTracker.GetOriginalValue(p => p.Description));

        // Act 4 - Rollback nested item
        itemTracker.Rollback();

        // Assert 4 - Nested item restored
        Assert.Equal(initialQuantity, item.Quantity);
        Assert.False(itemTracker.IsDirty());

        // Product changes not rolled back (separate tracker)
        Assert.True(productTracker.IsDirty());
    }

    [Fact]
    public void DeepTracking_CollectionWithNestedTrackableObjects()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { Quantity = 1, UnitPrice = 10m },
                new() { Quantity = 2, UnitPrice = 20m }
            }
        }.AsTrackable();

        var orderTracker = order.GetChangeTracker();

        // Act 1 - Add new item to collection
        order.Items.AsTrackable().Add(new OrderItem { Quantity = 3, UnitPrice = 30m });

        // Assert 1 - Collection dirty
        Assert.True(order.Items.AsTrackable().IsDirty());
        Assert.False(orderTracker.IsDirty());

        // Act 2 - Modify existing item in collection
        order.Items[0].Quantity = 10;

        // Assert 2 - Item dirty, collection dirty
        Assert.True(order.Items[0].GetChangeTracker().IsDirty());
        Assert.True(order.Items.AsTrackable().IsDirty());

        // Act 3 - Modify multiple items
        order.Items[1].UnitPrice = 25m;

        // Assert 3 - Multiple items tracked independently
        Assert.True(order.Items[0].GetChangeTracker().IsDirty());
        Assert.True(order.Items[1].GetChangeTracker().IsDirty());
        Assert.Equal(1, order.Items[0].GetChangeTracker().GetOriginalValue(i => i.Quantity));
        Assert.Equal(20m, order.Items[1].GetChangeTracker().GetOriginalValue(i => i.UnitPrice));
    }
}

public class TrackingModeTests
{
    [Fact]
    public void TrackingMode_All_Behavior()
    {
        // Arrange - Order uses TrackingMode.All (default)
        var order = new Order
        {
            Id = "ORD-001",
            CreatedDate = DateTime.Today,
            Notes = "Test notes"
        }.AsTrackable();

        var tracker = order.GetChangeTracker();

        // Act - Modify tracked property
        order.Id = "ORD-002";

        // Assert 1 - Partial properties tracked by default
        Assert.True(tracker.IsDirty());
        Assert.True(tracker.HasChanged(nameof(Order.Id)));

        // Act 2 - Modify [NotTracked] property
        order.CreatedDate = DateTime.Today.AddDays(1);

        // Assert 2 - [NotTracked] properties excluded
        Assert.False(tracker.HasChanged(nameof(Order.CreatedDate)));
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.CreatedDate)));

        // Assert 3 - Non-partial properties not tracked
        order.Notes = "Modified";
        Assert.False(tracker.GetOriginalValues().ContainsKey(nameof(Order.Notes)));
    }

    [Fact]
    public void TrackingMode_OnlyMarked_Behavior()
    {
        // Arrange - Product uses TrackingMode.OnlyMarked
        var product = new Product
        {
            Description = "Original Description"
        }.AsTrackable();

        var tracker = product.GetChangeTracker();

        // Act 1 - Modify [TrackOnly] property
        product.Description = "Updated Description";

        // Assert 1 - Only marked properties tracked
        Assert.True(tracker.IsDirty());
        Assert.True(tracker.HasChanged(nameof(Product.Description)));
        Assert.Equal("Original Description", tracker.GetOriginalValue(p => p.Description));

        // Act 2 - Modify [TrackOnly] collection
        product.Categories = new List<string> { "Category1" };
        product.Categories.AsTrackable().Add("Category2");

        // Assert 2 - Marked collections also tracked
        Assert.True(product.Categories.AsTrackable().IsDirty());
        Assert.Equal(2, product.Categories.Count);
    }
}

public class ChangeManagementTests
{
    [Fact]
    public void ChangeManagement_CompleteWorkflow()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001",
            Tags = new List<string> { "Tag1" }
        }.AsTrackable();

        var tracker = order.GetChangeTracker();

        // Act 1 - Make changes to property and collection
        order.Id = "ORD-002";
        order.Tags = new List<string> { "Tag2", "Tag3" };

        // Assert 1 - Changes tracked
        Assert.True(tracker.IsDirty());
        Assert.True(tracker.HasChanged(nameof(Order.Id)));
        Assert.True(tracker.HasChanged(nameof(Order.Tags)));
        Assert.Equal("ORD-001", tracker.GetOriginalValue(x => x.Id));

        // Act 2 - Rollback changes
        tracker.Rollback();

        // Assert 2 - All changes restored
        Assert.Equal("ORD-001", order.Id);
        Assert.Single(order.Tags);
        Assert.Contains("Tag1", order.Tags);
        Assert.False(tracker.IsDirty());

        // Act 3 - Make new changes
        order.Id = "ORD-003";
        order.Tags.AsTrackable().Add("Tag2");

        // Assert 3 - New changes tracked
        Assert.True(tracker.IsDirty());
        Assert.Equal("ORD-001", tracker.GetOriginalValue(x => x.Id));

        // Act 4 - AcceptChanges to establish new baseline
        tracker.AcceptChanges();

        // Assert 4 - Clean state, changes accepted as new baseline
        Assert.Equal("ORD-003", order.Id);
        Assert.Equal(2, order.Tags.Count);
        Assert.False(tracker.IsDirty());

        // Act 5 - Make changes after accepting
        order.Id = "ORD-004";

        // Assert 5 - Original value is now the accepted value
        Assert.True(tracker.IsDirty());
        Assert.Equal("ORD-003", tracker.GetOriginalValue(x => x.Id));
        Assert.Equal("ORD-004", order.Id);

        // Act 6 - Rollback to accepted baseline
        tracker.Rollback();

        // Assert 6 - Restored to accepted baseline
        Assert.Equal("ORD-003", order.Id);
        Assert.False(tracker.IsDirty());
    }
}

public class ChangeTrackerApiTests
{
    [Fact]
    public void ChangeTracker_Api_Usage()
    {
        // Act 1 - AsTrackable extension
        var order = new Order { Id = "ORD-001" }.AsTrackable();
        Assert.NotNull(order);

        // Act 2 - TryGetChangeTracker returns tracker
        var tracker = order.TryGetChangeTracker();
        Assert.NotNull(tracker);

        // Act 3 - GetChangeTracker returns tracker
        var tracker2 = order.GetChangeTracker();
        Assert.NotNull(tracker2);
        Assert.Same(tracker, tracker2);

        // Act 4 - Change property and use HasChanged variations
        order.Id = "ORD-002";

        Assert.True(tracker.HasChanged(nameof(Order.Id))); // By name
        Assert.True(tracker.HasChanged(x => x.Id)); // By expression

        // Act 5 - GetOriginalValue variations
        Assert.Equal("ORD-001", tracker.GetOriginalValue(x => x.Id)); // By expression
        Assert.Equal("ORD-001", tracker.GetOriginalValues()[nameof(Order.Id)]); // From dictionary

        // Act 6 - IsDirty variations
        Assert.True(tracker.IsDirty());
        Assert.True(tracker.IsDirty(deepTracking: false));

        // Act 7 - GetOriginalValues returns dictionary
        var originalValues = tracker.GetOriginalValues();
        Assert.NotNull(originalValues);
        Assert.IsType<Dictionary<string, object?>>(originalValues);
        Assert.Single(originalValues);

        // Act 8 - Collection AsTrackable
        order.Tags = new List<string> { "Tag1" };
        var trackableList = order.Tags.AsTrackable();
        Assert.NotNull(trackableList);
        Assert.IsType<TrackableList<string>>(trackableList);

        // Act 9 - Collection TryGetChangeTracker
        var listTracker = order.Tags.TryGetChangeTracker();
        Assert.NotNull(listTracker);

        // Act 10 - Dictionary AsTrackable
        order.Options = new Dictionary<string, string> { { "Key", "Value" } };
        var trackableDict = order.Options.AsTrackable();
        Assert.NotNull(trackableDict);
        Assert.IsType<TrackableDictionary<string, string>>(trackableDict);

        // Act 11 - Dictionary TryGetChangeTracker
        var dictTracker = order.Options.TryGetChangeTracker();
        Assert.NotNull(dictTracker);
    }

    [Fact]
    public void ChangeTracker_ErrorHandling()
    {
        // Arrange - Create order without tracking
        var order = new Order { Id = "ORD-001" };

        // Act & Assert 1 - TryGetChangeTracker returns null when not initialized
        var tracker = order.TryGetChangeTracker();
        Assert.Null(tracker);

        // Act & Assert 2 - GetChangeTracker throws when not initialized
        var exception = Assert.Throws<InvalidOperationException>(() => order.GetChangeTracker());
        Assert.Contains("not initialized", exception.Message);

        // Act & Assert 3 - AsTrackable on regular dictionary throws
        var regularDict = new Dictionary<string, OrderItem>();
        var dictException = Assert.Throws<ArgumentException>(() => regularDict.AsTrackable());
        Assert.Contains("not TrackableDictionary", dictException.Message);

        // Act & Assert 4 - AsTrackable on regular list throws
        var regularList = new List<OrderItem>();
        var listException = Assert.Throws<ArgumentException>(() => regularList.AsTrackable());
        Assert.Contains("not TrackableList", listException.Message);
    }
}