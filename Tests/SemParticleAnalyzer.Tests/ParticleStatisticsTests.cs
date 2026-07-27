using SemParticleAnalyzer.Services;

namespace SemParticleAnalyzer.Tests;

public sealed class ParticleStatisticsTests
{
    [Theory]
    [InlineData(0.1, 1.9)]
    [InlineData(0.5, 5.5)]
    [InlineData(0.9, 9.1)]
    public void Percentile_InterpolatesSortedValues(double percentile, double expected)
    {
        double?[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(expected, ParticleStatistics.Percentile(values, percentile)!.Value, 8);
    }

    [Fact]
    public void Histogram_PreservesTotalCount()
    {
        double?[] values = [1, 2, 2, 3, 5, 8, null, double.NaN];
        var bins = ParticleStatistics.Histogram(values, 4);

        Assert.Equal(6, bins.Sum(x => x.Count));
        Assert.Equal(1, bins.Sum(x => x.Fraction), 8);
    }
}
