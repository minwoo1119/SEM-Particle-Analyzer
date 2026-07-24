using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.Services;

namespace SemParticleAnalyzer.Tests;

public sealed class CalibrationServiceTests
{
    private readonly CalibrationService _service = new();

    [Fact]
    public void CalculateImageScale_ConvertsMicrometerWidth()
    {
        var calibration = new ScaleCalibration
        {
            ActualImageWidth = 200,
            InputUnit = LengthUnit.Micrometer
        };

        var scale = _service.CalculateImageScale(1000, 500, calibration);

        Assert.Equal(0.2, scale, 10);
    }

    [Theory]
    [InlineData(200_000, LengthUnit.Nanometer)]
    [InlineData(0.2, LengthUnit.Millimeter)]
    public void CalculateImageScale_NormalizesUnitsToMicrometer(double width, LengthUnit unit)
    {
        var calibration = new ScaleCalibration { ActualImageWidth = width, InputUnit = unit };

        Assert.Equal(0.2, _service.CalculateImageScale(1000, 500, calibration), 10);
    }

    [Fact]
    public void CalculateImageScale_RejectsInconsistentAxes()
    {
        var calibration = new ScaleCalibration
        {
            ActualImageWidth = 200,
            ActualImageHeight = 50,
            InputUnit = LengthUnit.Micrometer
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _service.CalculateImageScale(1000, 500, calibration));

        Assert.Contains("1%", exception.Message);
    }

    [Fact]
    public void CalculateImageScale_AcceptsConsistentAxes()
    {
        var calibration = new ScaleCalibration
        {
            ActualImageWidth = 200,
            ActualImageHeight = 100,
            InputUnit = LengthUnit.Micrometer
        };

        Assert.Equal(0.2, _service.CalculateImageScale(1000, 500, calibration), 10);
    }
}
