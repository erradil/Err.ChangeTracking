using Err.ChangeTracking.SampleDemo.Models;

namespace Err.ChangeTracking.Tests;

public class CollectionTrackingTests
{
    [Fact]
    public void TrackableList_DetectsStructuralChanges()
    {
        // Arrange
        var model = new ModelWithList
        {
            Items = []
        }.AsTrackable();

        // Act - Add an item to the list using AsTrackable() to access the overridden methods
        model.Items?.AsTrackable().Add("New Item");

        // Assert - Only check the collection's dirty state, not the model
        Assert.True(model.Items?.AsTrackable().IsDirty);
        // Model doesn't get notified of collection changes directly
        Assert.False(model.GetChangeTracker().IsDirty);
    }

    [Fact]
    public void TrackableList_DetectsChangesAfterInitialization()
    {
        // Arrange
        var model = new ModelWithList
        {
            Items = ["Item 1", "Item 2"]
        }.AsTrackable();

        // Act - Use AsTrackable() to access the overridden methods
        model.Items?.AsTrackable().Add("Item 3");

        // Assert - Only check the collection's dirty state
        Assert.True(model.Items?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableList_RemovingItemDetected()
    {
        // Arrange
        var model = new ModelWithList
        {
            Items = ["Item 1", "Item 2"]
        }.AsTrackable();

        // Act - Use AsTrackable() to access the overridden methods
        model.Items?.AsTrackable().Remove("Item 1");

        // Assert - Only check the collection's dirty state
        Assert.True(model.Items?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableList_ClearingItemsDetected()
    {
        // Arrange
        var model = new ModelWithList
        {
            Items = ["Item 1", "Item 2"]
        }.AsTrackable();

        // Act - Use AsTrackable() to access the overridden methods
        model.Items?.AsTrackable().Clear();

        // Assert - Only check the collection's dirty state
        Assert.True(model.Items?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableList_AddRangeDetected()
    {
        // Arrange
        var model = new ModelWithList { Items = [] }.AsTrackable();

        // Act - Use AsTrackable() to access the overridden methods
        model.Items?.AsTrackable().AddRange(["Item 1", "Item 2"]);

        // Assert - Only check the collection's dirty state
        Assert.True(model.Items?.AsTrackable().IsDirty);
        Assert.Equal(2, model.Items?.Count);
    }

    [Fact]
    public void TrackableList_NestedTrackableObjectsDetected()
    {
        // Arrange
        var model = new ModelWithNestedList
        {
            NestedItems = new List<NestedItem>
            {
                new() { Name = "Item 1" },
                new() { Name = "Item 2" }
            }
        }.AsTrackable();

        // Act - Modify a property of a nested object
        // No need for AsTrackable on individual items as they aren't collections
        model.NestedItems.AsTrackable()[0].Name = "Modified Item";

        // Assert - The change should be detected in the collection
        Assert.True(model.NestedItems?.AsTrackable().IsDirty);
        Assert.True(model.NestedItems![0].GetChangeTracker().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_DetectsStructuralChanges()
    {
        // Arrange
        var model = new ModelWithDictionary { Properties = [] }.AsTrackable();

        // Act - Add an item to the dictionary using AsTrackable
        model.Properties?.AsTrackable().Add("Key1", "Value1");

        // Assert - Only check the collection's dirty state
        Assert.True(model.Properties?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_DetectsChangesAfterInitialization()
    {
        // Arrange
        var model = new ModelWithDictionary
        {
            Properties = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        }.AsTrackable();

        // Act - Use AsTrackable to access the overridden methods
        model.Properties?.AsTrackable().Add("Key3", "Value3");

        // Assert - Only check the collection's dirty state
        Assert.True(model.Properties?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_UpdatingExistingValueDetected()
    {
        // Arrange
        var model = new ModelWithDictionary
        {
            Properties = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        }.AsTrackable();

        // Act - Use indexer with AsTrackable
        model.Properties.AsTrackable()["Key1"] = "Updated Value";

        // Assert - Only check the collection's dirty state
        Assert.True(model.Properties?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_RemovingItemDetected()
    {
        // Arrange
        var model = new ModelWithDictionary
        {
            Properties = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        }.AsTrackable();

        // Act - Use AsTrackable to access the overridden methods
        model.Properties?.AsTrackable().Remove("Key1");

        // Assert - Only check the collection's dirty state
        Assert.True(model.Properties?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_ClearingItemsDetected()
    {
        // Arrange
        var model = new ModelWithDictionary
        {
            Properties = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        }.AsTrackable();

        // Act - Use AsTrackable to access the overridden methods
        model.Properties?.AsTrackable().Clear();

        // Assert - Only check the collection's dirty state
        Assert.True(model.Properties?.AsTrackable().IsDirty);
    }

    [Fact]
    public void TrackableDictionary_NestedTrackableObjectsDetected()
    {
        // Arrange
        var model = new ModelWithNestedDictionary
        {
            ConfigItems = new Dictionary<string, NestedItem>
            {
                { "Item1", new NestedItem { Name = "Config 1" } },
                { "Item2", new NestedItem { Name = "Config 2" } }
            }
        }.AsTrackable();

        // Act - Modify a property of a nested object
        // No need for AsTrackable on individual items as they aren't collections
        model.ConfigItems["Item1"].Name = "Modified Config";

        // Assert - The change should be detected in the collection
        Assert.True(model.ConfigItems?.AsTrackable().IsDirty);
    }

    [Fact]
    public void Model_DetectsChangesWhenCollectionsAreReplaced()
    {
        // Arrange
        var model = new ComplexModel
        {
            Name = "Test Model",
            Items = new List<string> { "Item 1" }
        }.AsTrackable();

        // Act - Replace the entire collection (this should be tracked by the model)
        model.Items = new List<string> { "New Item 1", "New Item 2" };

        // Assert - The model should detect the replacement of the collection
        Assert.True(model.GetChangeTracker().IsDirty);
    }

    [Fact]
    public void NullableTrackableList_HandlesNullCorrectly()
    {
        // Arrange
        var model = new ModelWithNullableList().AsTrackable();

        // Act & Assert - Initially null
        Assert.Null(model.OptionalItems);

        // Set to non-null and modify with AsTrackable
        model.OptionalItems = new List<string>();
        // Setting OptionalItems should make the model dirty
        Assert.True(model.GetChangeTracker().IsDirty);

        model.OptionalItems?.AsTrackable().Add("Item1");

        // Assert - Collection should be dirty now
        Assert.NotNull(model.OptionalItems);
        Assert.True(model.OptionalItems?.AsTrackable().IsDirty);

        // Set back to null - should make model dirty again if it was accepted
        model.GetChangeTracker().AcceptChanges();
        Assert.False(model.GetChangeTracker().IsDirty);

        model.OptionalItems = null;

        // Assert - Model should detect the change to null
        Assert.Null(model.OptionalItems);
        Assert.True(model.GetChangeTracker().IsDirty);
    }

    [Fact]
    public void NullableTrackableDictionary_HandlesNullCorrectly()
    {
        // Arrange
        var model = new ModelWithNullableDictionary().AsTrackable();

        // Act & Assert - Initially null
        Assert.Null(model.OptionalProperties);

        // Set to non-null - should make model dirty
        model.OptionalProperties = new Dictionary<string, string>();
        Assert.True(model.GetChangeTracker().IsDirty);

        model.OptionalProperties?.AsTrackable().Add("Key1", "Value1");

        // Assert
        Assert.NotNull(model.OptionalProperties);
        Assert.True(model.OptionalProperties?.AsTrackable().IsDirty);

        // Set back to null - should make model dirty again if it was accepted
        model.GetChangeTracker().AcceptChanges();
        Assert.False(model.GetChangeTracker().IsDirty);

        model.OptionalProperties = null;

        // Assert - Model should detect the change to null
        Assert.Null(model.OptionalProperties);
        Assert.True(model.GetChangeTracker().IsDirty);
    }

    [Fact]
    public void ModelRollback_RestoresCollections()
    {
        // Arrange
        var model = new ComplexModel
        {
            Name = "Test Model",
            Items = new List<string> { "Item 1", "Item 2" },
            Properties = new Dictionary<string, string>
            {
                { "Prop1", "Value 1" },
                { "Prop2", "Value 2" }
            }
        }.AsTrackable();

        // Act - Replace collections entirely (model should track this)
        model.Name = "Modified Name";
        model.Items = new List<string> { "New Item" };
        model.Properties = new Dictionary<string, string> { { "NewKey", "NewValue" } };

        // Rollback all changes
        model.GetChangeTracker().Rollback();

        // Assert
        Assert.Equal("Test Model", model.Name);
        Assert.Equal(2, model.Items?.Count);
        Assert.Equal(2, model.Properties?.Count);
        Assert.Contains("Item 1", model.Items!);
        Assert.Contains("Item 2", model.Items!);
        Assert.True(model.Properties!.ContainsKey("Prop1"));
        Assert.False(model.GetChangeTracker().IsDirty);
    }

    [Fact]
    public void NullableCollection_Rollback_RestoresToNull()
    {
        // Arrange
        var model = new ModelWithNullableCollections().AsTrackable();
        Assert.Null(model.OptionalItems);
        Assert.Null(model.OptionalProperties);

        // Act - Set collections (model tracks these assignments)
        model.OptionalItems = new List<string>();
        model.OptionalProperties = new Dictionary<string, string>();

        // Rollback
        model.GetChangeTracker().Rollback();

        // Assert - Both collections should be back to null
        Assert.Null(model.OptionalItems);
        Assert.Null(model.OptionalProperties);
        Assert.False(model.GetChangeTracker().IsDirty);
    }
}