using OpenCvSharp;
using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.Services;

namespace SemParticleAnalyzer.Tests;

public sealed class AnalysisServiceTests
{
    [Fact]
    public async Task Analyze_DetectsCircleAndRecordsFilterRejection()
    {
        using var image = Mat.Zeros(new Size(200, 160), MatType.CV_8UC1).ToMat();
        Cv2.Circle(image, new Point(70, 80), 15, Scalar.White, -1);
        Cv2.Circle(image, new Point(140, 80), 4, Scalar.White, -1);
        var settings = new AnalysisSettings
        {
            Roi = new RectangleRoi { X = 10, Y = 10, Width = 180, Height = 140 },
            MinimumGv = 200,
            MaximumGv = 255,
            AreaFilter = new RangeFilter { Enabled = true, Minimum = 100, Unit = "px²" }
        };

        using var result = await new AnalysisService().AnalyzeAsync(image, settings, CancellationToken.None);

        Assert.Equal(2, result.Summary.SegmentedCount);
        Assert.Equal(1, result.Summary.AcceptedCount);
        Assert.Contains(result.Objects, x => !x.FinalAccepted && x.RejectedBy.Contains("Area"));
        Assert.Contains(result.Objects, x => x.FinalAccepted && x.Circularity.HasValue);
    }

    [Fact]
    public async Task Analyze_ExcludesObjectTouchingRoiBoundary()
    {
        using var image = Mat.Zeros(new Size(100, 100), MatType.CV_8UC1).ToMat();
        Cv2.Rectangle(image, new Rect(10, 30, 15, 15), Scalar.White, -1);
        var settings = new AnalysisSettings
        {
            Roi = new RectangleRoi { X = 10, Y = 10, Width = 80, Height = 80 },
            MinimumGv = 200,
            MaximumGv = 255,
            BorderRule = BorderObjectRule.Exclude,
            AreaFilter = new RangeFilter()
        };

        using var result = await new AnalysisService().AnalyzeAsync(image, settings, CancellationToken.None);

        var particle = Assert.Single(result.Objects);
        Assert.True(particle.TouchesBorder);
        Assert.False(particle.FinalAccepted);
        Assert.Contains("BorderContact", particle.RejectedBy);
    }

    [Fact]
    public async Task Analyze_ConvertsPixelMeasurementsWithEnabledCalibration()
    {
        using var image = Mat.Zeros(new Size(100, 100), MatType.CV_8UC1).ToMat();
        Cv2.Circle(image, new Point(50, 50), 10, Scalar.White, -1);
        var settings = new AnalysisSettings
        {
            Roi = new RectangleRoi { Width = 100, Height = 100 },
            MinimumGv = 200,
            MaximumGv = 255,
            AreaFilter = new RangeFilter(),
            Calibration = new ScaleCalibration { Enabled = true, MicrometersPerPixel = 0.5 }
        };

        using var result = await new AnalysisService().AnalyzeAsync(image, settings, CancellationToken.None);

        var particle = Assert.Single(result.Objects);
        Assert.Equal(particle.AreaPixel2 * 0.25, particle.AreaUm2!.Value, 8);
        Assert.Equal(particle.EquivalentDiameterPixel!.Value * 0.5, particle.EquivalentDiameterUm!.Value, 8);
    }
}
