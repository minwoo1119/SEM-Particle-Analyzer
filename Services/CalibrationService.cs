using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public sealed class CalibrationService : ICalibrationService
{
    public double CalculateImageScale(int pixelWidth, int pixelHeight, ScaleCalibration calibration)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "이미지 픽셀 크기가 유효하지 않습니다.");

        var unitFactor = calibration.InputUnit switch
        {
            LengthUnit.Nanometer => 0.001,
            LengthUnit.Millimeter => 1000,
            _ => 1
        };
        double? horizontal = calibration.ActualImageWidth is > 0
            ? calibration.ActualImageWidth.Value * unitFactor / pixelWidth : null;
        double? vertical = calibration.ActualImageHeight is > 0
            ? calibration.ActualImageHeight.Value * unitFactor / pixelHeight : null;

        if (horizontal is null && vertical is null)
            throw new InvalidOperationException("실제 이미지 너비 또는 높이를 하나 이상 입력하세요.");
        if (horizontal is not null && vertical is not null)
        {
            var difference = Math.Abs(horizontal.Value - vertical.Value);
            var average = (horizontal.Value + vertical.Value) / 2;
            if (difference / average > 0.01)
                throw new InvalidOperationException("입력한 실제 너비와 높이의 축척이 1% 이상 다릅니다. 이미지 비율과 단위를 확인하세요.");
            return average;
        }
        return horizontal ?? vertical!.Value;
    }
}
