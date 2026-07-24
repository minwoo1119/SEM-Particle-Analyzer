using System.Text.Json.Serialization;

namespace SemParticleAnalyzer.Models;

public enum ThresholdMode { InRange, Binary, Otsu }
public enum BorderObjectRule { Include, Exclude, Mark }
public enum ManualOverrideType { None, Include, Exclude }
public enum ViewerMode { Original, Preprocessed, BinaryMask, Overlay }
public enum ViewerTool { SelectAndRoi, Pan, ZoomArea }
public enum LengthUnit { Nanometer, Micrometer, Millimeter }

public sealed class RangeFilter
{
    public bool Enabled { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Unit { get; set; } = "";
    public bool Inclusive { get; set; } = true;

    public bool Accepts(double? value)
    {
        if (!Enabled) return true;
        if (!value.HasValue || double.IsNaN(value.Value)) return false;
        return (Minimum is null || (Inclusive ? value >= Minimum : value > Minimum))
            && (Maximum is null || (Inclusive ? value <= Maximum : value < Maximum));
    }
}

public sealed class RectangleRoi
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    [JsonIgnore] public bool IsValid => Width > 0 && Height > 0;
}

public sealed class ScaleCalibration
{
    public bool Enabled { get; set; }
    public double? ActualImageWidth { get; set; }
    public double? ActualImageHeight { get; set; }
    public LengthUnit InputUnit { get; set; } = LengthUnit.Micrometer;
    public double PixelDistance { get; set; }
    public double ActualLength { get; set; }
    public string Unit { get; set; } = "µm";
    public double? MicrometersPerPixel { get; set; }
}

public sealed class PreprocessingSettings
{
    public bool GaussianEnabled { get; set; }
    public int GaussianKernelSize { get; set; } = 3;
    public bool ClaheEnabled { get; set; }
    public double ClaheClipLimit { get; set; } = 2;
    public bool Invert { get; set; }
    public bool MorphologyOpenEnabled { get; set; }
    public int MorphologyKernelSize { get; set; } = 3;
}

public sealed class AnalysisSettings
{
    public int SchemaVersion { get; set; } = 1;
    public RectangleRoi Roi { get; set; } = new();
    public ScaleCalibration Calibration { get; set; } = new();
    public PreprocessingSettings Preprocessing { get; set; } = new();
    public ThresholdMode ThresholdMode { get; set; } = ThresholdMode.InRange;
    public int MinimumGv { get; set; } = 135;
    public int MaximumGv { get; set; } = 255;
    public BorderObjectRule BorderRule { get; set; } = BorderObjectRule.Mark;
    public int MinimumShapeMeasurementArea { get; set; } = 9;
    public RangeFilter AreaFilter { get; set; } = new() { Enabled = true, Minimum = 5, Unit = "px²" };
    public RangeFilter EquivalentDiameterFilter { get; set; } = new() { Unit = "px" };
    public RangeFilter CircularityFilter { get; set; } = new() { Unit = "ratio" };
    public RangeFilter SolidityFilter { get; set; } = new() { Unit = "ratio" };
}

public sealed class SourceImageInfo
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Channels { get; set; }
    public string Depth { get; set; } = "";
}

public sealed class ParticleMeasurement
{
    public int ObjectId { get; set; }
    public bool AutomaticAccepted { get; set; }
    public ManualOverrideType ManualOverride { get; set; }
    public bool FinalAccepted => ManualOverride switch
    {
        ManualOverrideType.Include => true,
        ManualOverrideType.Exclude => false,
        _ => AutomaticAccepted
    };
    public List<string> RejectedBy { get; set; } = [];
    public double CentroidXPixel { get; set; }
    public double CentroidYPixel { get; set; }
    public int BoundingBoxX { get; set; }
    public int BoundingBoxY { get; set; }
    public int BoundingBoxWidth { get; set; }
    public int BoundingBoxHeight { get; set; }
    public double AreaPixel2 { get; set; }
    public double? AreaUm2 { get; set; }
    public double PerimeterPixel { get; set; }
    public double? EquivalentDiameterPixel { get; set; }
    public double? EquivalentDiameterUm { get; set; }
    public double? Circularity { get; set; }
    public double? Solidity { get; set; }
    public double MeanGv { get; set; }
    public double MinGv { get; set; }
    public double MaxGv { get; set; }
    public double StdDevGv { get; set; }
    public bool TouchesBorder { get; set; }
    public string RejectionSummary => string.Join(", ", RejectedBy);
}

public sealed class AnalysisSummary
{
    public int SegmentedCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount => SegmentedCount - AcceptedCount;
    public double RoiAreaPixel2 { get; set; }
    public double AcceptedAreaPixel2 { get; set; }
    public double AreaFraction => RoiAreaPixel2 <= 0 ? 0 : AcceptedAreaPixel2 / RoiAreaPixel2;
    public TimeSpan ProcessingTime { get; set; }
}
