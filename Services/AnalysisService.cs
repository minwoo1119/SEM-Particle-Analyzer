using System.Diagnostics;
using OpenCvSharp;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public sealed class AnalysisService : IAnalysisService
{
    public Task<AnalysisResult> AnalyzeAsync(Mat source, AnalysisSettings settings, CancellationToken cancellationToken) =>
        Task.Run(() => Analyze(source, settings, cancellationToken), cancellationToken);

    private static AnalysisResult Analyze(Mat source, AnalysisSettings settings, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var roi = NormalizeRoi(settings.Roi, source.Size());
        if (roi.Width <= 0 || roi.Height <= 0) throw new InvalidOperationException("분석 ROI가 유효하지 않습니다.");

        using var gray = ToGray8(source);
        var preprocessed = ApplyPreprocessing(gray, settings.Preprocessing);
        var binaryFull = Mat.Zeros(source.Size(), MatType.CV_8UC1).ToMat();
        using (var grayRoi = new Mat(preprocessed, roi))
        using (var binaryRoi = new Mat(binaryFull, roi))
            ApplyThreshold(grayRoi, binaryRoi, settings);

        token.ThrowIfCancellationRequested();
        var objects = MeasureObjects(gray, binaryFull, roi, settings, token);
        var overlay = RenderOverlay(source, roi, binaryFull, objects);
        watch.Stop();
        return new AnalysisResult
        {
            Objects = objects,
            Preprocessed = preprocessed,
            BinaryMask = binaryFull,
            Overlay = overlay,
            Summary = new AnalysisSummary
            {
                SegmentedCount = objects.Count,
                AcceptedCount = objects.Count(x => x.FinalAccepted),
                RoiAreaPixel2 = roi.Width * roi.Height,
                AcceptedAreaPixel2 = objects.Where(x => x.FinalAccepted).Sum(x => x.AreaPixel2),
                ProcessingTime = watch.Elapsed
            }
        };
    }

    private static Rect NormalizeRoi(RectangleRoi value, Size size)
    {
        if (!value.IsValid) return new Rect(0, 0, size.Width, size.Height);
        var x = Math.Clamp(value.X, 0, size.Width);
        var y = Math.Clamp(value.Y, 0, size.Height);
        var right = Math.Clamp(value.X + value.Width, 0, size.Width);
        var bottom = Math.Clamp(value.Y + value.Height, 0, size.Height);
        return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private static Mat ToGray8(Mat source)
    {
        var gray = new Mat();
        if (source.Channels() == 1) source.CopyTo(gray);
        else Cv2.CvtColor(source, gray, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        if (gray.Depth() == MatType.CV_8U) return gray;
        var converted = new Mat();
        Cv2.Normalize(gray, converted, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        gray.Dispose();
        return converted;
    }

    private static Mat ApplyPreprocessing(Mat gray, PreprocessingSettings settings)
    {
        var current = gray.Clone();
        if (settings.GaussianEnabled)
        {
            var k = Odd(settings.GaussianKernelSize);
            Cv2.GaussianBlur(current, current, new Size(k, k), 0);
        }
        if (settings.ClaheEnabled)
        {
            using var clahe = Cv2.CreateCLAHE(Math.Max(.1, settings.ClaheClipLimit), new Size(8, 8));
            clahe.Apply(current, current);
        }
        if (settings.Invert) Cv2.BitwiseNot(current, current);
        return current;
    }

    private static void ApplyThreshold(Mat input, Mat output, AnalysisSettings settings)
    {
        if (settings.ThresholdMode == ThresholdMode.Otsu)
            Cv2.Threshold(input, output, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        else if (settings.ThresholdMode == ThresholdMode.Binary)
            Cv2.Threshold(input, output, settings.MinimumGv, 255, ThresholdTypes.Binary);
        else
            Cv2.InRange(input, new Scalar(settings.MinimumGv), new Scalar(settings.MaximumGv), output);

        if (settings.Preprocessing.MorphologyOpenEnabled)
        {
            var k = Odd(settings.Preprocessing.MorphologyKernelSize);
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
            Cv2.MorphologyEx(output, output, MorphTypes.Open, kernel);
        }
    }

    private static List<ParticleMeasurement> MeasureObjects(Mat gray, Mat mask, Rect roi,
        AnalysisSettings settings, CancellationToken token)
    {
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var result = new List<ParticleMeasurement>();
        var ordered = contours.OrderBy(c => Cv2.BoundingRect(c).Y).ThenBy(c => Cv2.BoundingRect(c).X);
        foreach (var contour in ordered)
        {
            token.ThrowIfCancellationRequested();
            var area = Cv2.ContourArea(contour);
            if (area <= 0) continue;
            var bounds = Cv2.BoundingRect(contour);
            var perimeter = Cv2.ArcLength(contour, true);
            var moments = Cv2.Moments(contour);
            var equivalent = 2 * Math.Sqrt(area / Math.PI);
            var reliable = area >= settings.MinimumShapeMeasurementArea && perimeter > 0;
            double? circularity = reliable ? 4 * Math.PI * area / (perimeter * perimeter) : null;
            var hull = Cv2.ConvexHull(contour);
            var hullArea = hull.Length >= 3 ? Cv2.ContourArea(hull) : 0;
            double? solidity = reliable && hullArea > 0 ? area / hullArea : null;
            var touches = bounds.X <= roi.X || bounds.Y <= roi.Y || bounds.Right >= roi.Right || bounds.Bottom >= roi.Bottom;

            using var objectMask = Mat.Zeros(mask.Size(), MatType.CV_8UC1).ToMat();
            Cv2.DrawContours(objectMask, [contour], -1, Scalar.White, -1);
            Cv2.MeanStdDev(gray, out var mean, out var std, objectMask);
            Cv2.MinMaxLoc(gray, out var min, out var max, out _, out _, objectMask);
            var item = new ParticleMeasurement
            {
                ObjectId = result.Count + 1,
                CentroidXPixel = moments.M00 == 0 ? bounds.X + bounds.Width / 2d : moments.M10 / moments.M00,
                CentroidYPixel = moments.M00 == 0 ? bounds.Y + bounds.Height / 2d : moments.M01 / moments.M00,
                BoundingBoxX = bounds.X, BoundingBoxY = bounds.Y,
                BoundingBoxWidth = bounds.Width, BoundingBoxHeight = bounds.Height,
                AreaPixel2 = area,
                AreaUm2 = settings.Calibration.MicrometersPerPixel is { } scale ? area * scale * scale : null,
                PerimeterPixel = perimeter,
                EquivalentDiameterPixel = equivalent,
                EquivalentDiameterUm = settings.Calibration.MicrometersPerPixel is { } s ? equivalent * s : null,
                Circularity = circularity, Solidity = solidity,
                MeanGv = mean.Val0, MinGv = min, MaxGv = max, StdDevGv = std.Val0,
                TouchesBorder = touches
            };
            Evaluate(item, settings);
            result.Add(item);
        }
        return result;
    }

    private static void Evaluate(ParticleMeasurement item, AnalysisSettings settings)
    {
        if (!settings.AreaFilter.Accepts(item.AreaPixel2)) item.RejectedBy.Add("Area");
        if (!settings.EquivalentDiameterFilter.Accepts(item.EquivalentDiameterPixel)) item.RejectedBy.Add("EquivalentDiameter");
        if (!settings.CircularityFilter.Accepts(item.Circularity)) item.RejectedBy.Add("Circularity");
        if (!settings.SolidityFilter.Accepts(item.Solidity)) item.RejectedBy.Add("Solidity");
        if (item.TouchesBorder && settings.BorderRule == BorderObjectRule.Exclude) item.RejectedBy.Add("BorderContact");
        item.AutomaticAccepted = item.RejectedBy.Count == 0;
    }

    private static Mat RenderOverlay(Mat source, Rect roi, Mat mask, IReadOnlyList<ParticleMeasurement> objects)
    {
        var overlay = new Mat();
        if (source.Channels() == 1) Cv2.CvtColor(source, overlay, ColorConversionCodes.GRAY2BGR);
        else if (source.Channels() == 4) Cv2.CvtColor(source, overlay, ColorConversionCodes.BGRA2BGR);
        else source.CopyTo(overlay);
        if (overlay.Depth() != MatType.CV_8U)
        {
            using var normalized = new Mat();
            Cv2.Normalize(overlay, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8UC3.Value);
            normalized.CopyTo(overlay);
        }
        Cv2.Rectangle(overlay, roi, new Scalar(210, 210, 210), 2);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (var contour in contours)
        {
            var b = Cv2.BoundingRect(contour);
            var item = objects.FirstOrDefault(o => o.BoundingBoxX == b.X && o.BoundingBoxY == b.Y);
            if (item is null) continue;
            var color = item.FinalAccepted ? new Scalar(86, 174, 102) : new Scalar(82, 82, 200);
            Cv2.DrawContours(overlay, [contour], -1, color, 1, LineTypes.AntiAlias);
            Cv2.PutText(overlay, item.ObjectId.ToString(), new Point(b.X, Math.Max(10, b.Y - 3)),
                HersheyFonts.HersheySimplex, .35, color, 1, LineTypes.AntiAlias);
        }
        return overlay;
    }

    private static int Odd(int value) => Math.Max(1, value % 2 == 0 ? value + 1 : value);
}
