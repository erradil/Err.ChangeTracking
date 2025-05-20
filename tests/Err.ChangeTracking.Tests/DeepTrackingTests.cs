using Err.ChangeTracking.SampleDemo.Models;

namespace Err.ChangeTracking.Tests;

public class DeepTrackingTests
{
    [Fact]
    public void Model_DeepPropertyTracking()
    {
        // Arrange
        var model = new Model
        {
            Name = "ORD-001",
            SubModel = new SubModel
            {
                Name = "SubModel-001"
            }.AsTrackable()
        }.AsTrackable();

        // Act - Modify a simple tracked property
        model.SubModel.Name = "SubModel-xxx";
        var subModelTracker = model.SubModel.GetChangeTracker();
        var modelTracker = model.GetChangeTracker();
        // Assert
        Assert.True(subModelTracker.IsDirty(true));
        Assert.True(modelTracker.IsDirty(true));
    }

    [Fact]
    public void Model_DeepCollectionTracking()
    {
        // Arrange
        var model = new Model
        {
            Name = "ORD-001",
            Items =
            [
                new SubModel
                {
                    Name = "SubModel-001"
                }.AsTrackable(),
                new SubModel
                {
                    Name = "SubModel-002"
                }.AsTrackable()
            ]
        }.AsTrackable();

        // Act - Modify a simple tracked property
        model.Items![1].Name = "SubModel-xxx";
        var modelTracker = model.GetChangeTracker();
        // Assert
        Assert.True(modelTracker.IsDirty(true));
    }
}