using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.ViewModels;

namespace SemParticleAnalyzer.Tests;

public sealed class ObjectSelectionTests
{
    [Fact]
    public void SelectObjectAt_SelectsSmallestBoundingBoxAtPoint()
    {
        using var viewModel = new MainViewModel();
        var large = new ParticleMeasurement
        {
            ObjectId = 1, BoundingBoxX = 10, BoundingBoxY = 10,
            BoundingBoxWidth = 50, BoundingBoxHeight = 50
        };
        var small = new ParticleMeasurement
        {
            ObjectId = 2, BoundingBoxX = 20, BoundingBoxY = 20,
            BoundingBoxWidth = 10, BoundingBoxHeight = 10
        };
        viewModel.Objects.Add(large);
        viewModel.Objects.Add(small);

        var found = viewModel.SelectObjectAt(25, 25);

        Assert.True(found);
        Assert.Same(small, viewModel.SelectedObject);
    }

    [Fact]
    public void SelectObjectAt_ReturnsFalseOutsideObjects()
    {
        using var viewModel = new MainViewModel();
        viewModel.Objects.Add(new ParticleMeasurement
        {
            ObjectId = 1, BoundingBoxX = 10, BoundingBoxY = 10,
            BoundingBoxWidth = 20, BoundingBoxHeight = 20
        });

        Assert.False(viewModel.SelectObjectAt(80, 80));
        Assert.Null(viewModel.SelectedObject);
    }
}
