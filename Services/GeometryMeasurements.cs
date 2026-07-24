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
}
