using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Tests;

public sealed class RangeFilterTests
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(4.99, false)]
    [InlineData(10.01, false)]
    public void InclusiveFilter_AppliesBothBounds(double value, bool expected)
    {
        var filter = new RangeFilter { Enabled = true, Minimum = 5, Maximum = 10, Inclusive = true };
        Assert.Equal(expected, filter.Accepts(value));
    }

    [Fact]
    public void EnabledFilter_RejectsInvalidValue()
    {
        var filter = new RangeFilter { Enabled = true };
        Assert.False(filter.Accepts(null));
        Assert.False(filter.Accepts(double.NaN));
    }

    [Fact]
    public void DisabledFilter_DoesNotRejectInvalidValue()
    {
        var filter = new RangeFilter { Enabled = false };
        Assert.True(filter.Accepts(null));
    }
}
