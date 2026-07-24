using SemParticleAnalyzer.Services;

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
}
