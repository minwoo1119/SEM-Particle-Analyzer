using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public static class ParticleStatistics
{
    public static double? Percentile(IEnumerable<double?> source, double percentile)
    {
        var values = source.Where(x => x is not null && double.IsFinite(x.Value))
            .Select(x => x!.Value).Order().ToArray();
        if (values.Length == 0) return null;
        var position = Math.Clamp(percentile, 0, 1) * (values.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return values[lower];
        return values[lower] + (values[upper] - values[lower]) * (position - lower);
    }

    public static IReadOnlyList<HistogramBin> Histogram(IEnumerable<double?> source, int binCount = 12)
    {
        var values = source.Where(x => x is not null && double.IsFinite(x.Value))
            .Select(x => x!.Value).ToArray();
        if (values.Length == 0) return [];
        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-12)
            return [new HistogramBin { Minimum = min, Maximum = max, Count = values.Length, Fraction = 1 }];
        binCount = Math.Clamp(binCount, 1, 100);
        var width = (max - min) / binCount;
        var counts = new int[binCount];
        foreach (var value in values)
            counts[Math.Min(binCount - 1, (int)((value - min) / width))]++;
        return Enumerable.Range(0, binCount).Select(i => new HistogramBin
        {
            Minimum = min + width * i,
            Maximum = min + width * (i + 1),
            Count = counts[i],
            Fraction = (double)counts[i] / values.Length
        }).ToArray();
    }
}
