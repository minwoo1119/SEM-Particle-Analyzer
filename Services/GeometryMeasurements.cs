namespace SemParticleAnalyzer.Services;

/// <summary>
/// 입자 형상 지표의 단일 정의 지점입니다. 계산 불가 값은 0이 아닌 null을 반환합니다.
/// </summary>
public static class GeometryMeasurements
{
    /// <summary>Equivalent diameter = 2 × sqrt(area / π), 단위는 입력 면적 단위의 제곱근입니다.</summary>
    public static double? EquivalentDiameter(double area) =>
        area > 0 && double.IsFinite(area) ? 2 * Math.Sqrt(area / Math.PI) : null;

    /// <summary>Circularity = 4 × π × area / perimeter², 이상적인 원은 1에 가깝습니다.</summary>
    public static double? Circularity(double area, double perimeter) =>
        area > 0 && perimeter > 0 && double.IsFinite(area) && double.IsFinite(perimeter)
            ? 4 * Math.PI * area / (perimeter * perimeter)
            : null;

    /// <summary>Solidity = area / convex hull area, 일반적인 유효 범위는 0–1입니다.</summary>
    public static double? Solidity(double area, double convexHullArea) =>
        area > 0 && convexHullArea > 0 && double.IsFinite(area) && double.IsFinite(convexHullArea)
            ? area / convexHullArea
            : null;

    public static double? MaximumFeret(IReadOnlyList<OpenCvSharp.Point> convexHull)
    {
        if (convexHull.Count < 2) return null;
        var maximumSquared = 0d;
        for (var i = 0; i < convexHull.Count - 1; i++)
        for (var j = i + 1; j < convexHull.Count; j++)
        {
            var dx = convexHull[i].X - convexHull[j].X;
            var dy = convexHull[i].Y - convexHull[j].Y;
            maximumSquared = Math.Max(maximumSquared, (double)dx * dx + (double)dy * dy);
        }
        return Math.Sqrt(maximumSquared);
    }

    public static (double? Minimum, double? Major, double? Minor, double? AspectRatio, double? Orientation)
        Axes(IReadOnlyList<OpenCvSharp.Point> contour)
    {
        if (contour.Count < 3) return (null, null, null, null, null);
        var rectangle = OpenCvSharp.Cv2.MinAreaRect(contour);
        var minFeret = Math.Min(rectangle.Size.Width, rectangle.Size.Height);
        if (contour.Count < 5)
        {
            var major = Math.Max(rectangle.Size.Width, rectangle.Size.Height);
            return (minFeret, major, minFeret, minFeret > 0 ? major / minFeret : null, rectangle.Angle);
        }
        var ellipse = OpenCvSharp.Cv2.FitEllipse(contour);
        var ellipseMajor = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
        var ellipseMinor = Math.Min(ellipse.Size.Width, ellipse.Size.Height);
        var orientation = ellipse.Size.Width >= ellipse.Size.Height ? ellipse.Angle : ellipse.Angle + 90;
        if (orientation >= 180) orientation -= 180;
        return (minFeret, ellipseMajor, ellipseMinor,
            ellipseMinor > 0 ? ellipseMajor / ellipseMinor : null, orientation);
    }
}
