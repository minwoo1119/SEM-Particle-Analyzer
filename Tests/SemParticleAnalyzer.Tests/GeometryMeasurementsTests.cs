using SemParticleAnalyzer.Services;
using OpenCvSharp;

namespace SemParticleAnalyzer.Tests;

public sealed class GeometryMeasurementsTests
{
    [Fact]
    public void EquivalentDiameter_ReturnsKnownCircleDiameter()
    {
        var area = Math.PI * 25;
        Assert.Equal(10, GeometryMeasurements.EquivalentDiameter(area)!.Value, 10);
    }

    [Fact]
    public void Circularity_ReturnsOneForIdealCircle()
    {
        var radius = 5d;
        var value = GeometryMeasurements.Circularity(Math.PI * radius * radius, 2 * Math.PI * radius);
        Assert.Equal(1, value!.Value, 10);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(double.NaN, 10)]
    public void InvalidShapeInputs_ReturnNull(double area, double perimeter)
    {
        Assert.Null(GeometryMeasurements.Circularity(area, perimeter));
    }

    [Fact]
    public void MaximumFeret_ReturnsLongestHullDiagonal()
    {
        Point[] hull = [new(0, 0), new(40, 0), new(40, 10), new(0, 10)];

        var value = GeometryMeasurements.MaximumFeret(hull);

        Assert.Equal(Math.Sqrt(1700), value!.Value, 8);
    }

    [Fact]
    public void Axes_ReturnsMinimumFeretForRectangle()
    {
        Point[] contour = [new(0, 0), new(40, 0), new(40, 10), new(0, 10)];

        var axes = GeometryMeasurements.Axes(contour);

        Assert.Equal(10, axes.Minimum!.Value, 6);
        Assert.Equal(4, axes.AspectRatio!.Value, 6);
    }
}
