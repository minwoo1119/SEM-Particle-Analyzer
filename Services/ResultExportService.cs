using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using OpenCvSharp;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public sealed class ResultExportService : IResultExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<string> ExportAsync(string destinationRoot, Mat source, SourceImageInfo info,
        AnalysisSettings settings, AnalysisResult result, CancellationToken token)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var root = UniqueDirectory(Path.Combine(destinationRoot, $"Result_{stamp}"));
        var sourceDir = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var imagesDir = Directory.CreateDirectory(Path.Combine(root, "images")).FullName;
        var dataDir = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var reportDir = Directory.CreateDirectory(Path.Combine(root, "report")).FullName;
        token.ThrowIfCancellationRequested();

        File.Copy(info.FilePath, Path.Combine(sourceDir, info.FileName), false);
        Cv2.ImWrite(Path.Combine(imagesDir, "original.png"), source);
        Cv2.ImWrite(Path.Combine(imagesDir, "preprocessed.png"), result.Preprocessed);
        Cv2.ImWrite(Path.Combine(imagesDir, "binary_mask.png"), result.BinaryMask);
        using (var labeledOverlay = CreateLabeledOverlay(result))
            Cv2.ImWrite(Path.Combine(imagesDir, "detection_overlay.png"), labeledOverlay);

        await WriteJsonAsync(Path.Combine(dataDir, "analysis_settings.json"), settings, token);
        await WriteJsonAsync(Path.Combine(dataDir, "objects.json"), result.Objects, token);
        await WriteJsonAsync(Path.Combine(dataDir, "run_metadata.json"), new
        {
            AnalyzedAt = DateTimeOffset.Now,
            ProgramVersion = typeof(ResultExportService).Assembly.GetName().Version?.ToString(),
            Source = info,
            result.Summary
        }, token);
        await WriteCsvAsync(Path.Combine(dataDir, "objects.csv"), result.Objects, token);
        await WriteReportAsync(Path.Combine(reportDir, "result_report.html"), info, settings, result, token);
        return root;
    }

    private static async Task WriteCsvAsync(string path, IReadOnlyList<ParticleMeasurement> objects, CancellationToken token)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        await writer.WriteLineAsync("ObjectId,Accepted,AutomaticAccepted,ManualOverride,RejectedBy,CentroidX_px,CentroidY_px,Area_px2,Area_um2,Perimeter_px,EquivalentDiameter_px,EquivalentDiameter_um,MaxFeret_px,MinFeret_px,MaxFeret_um,MinFeret_um,MajorAxis_px,MinorAxis_px,AspectRatio,Orientation_deg,Circularity,Solidity,MeanGV,MinGV,MaxGV,StdDevGV,TouchesBorder,BoundingBoxX,BoundingBoxY,BoundingBoxWidth,BoundingBoxHeight");
        foreach (var x in objects)
        {
            token.ThrowIfCancellationRequested();
            var values = new object?[]
            {
                x.ObjectId, x.FinalAccepted, x.AutomaticAccepted, x.ManualOverride, x.RejectionSummary,
                x.CentroidXPixel, x.CentroidYPixel, x.AreaPixel2, x.AreaUm2, x.PerimeterPixel,
                x.EquivalentDiameterPixel, x.EquivalentDiameterUm,
                x.MaxFeretPixel, x.MinFeretPixel, x.MaxFeretUm, x.MinFeretUm,
                x.MajorAxisPixel, x.MinorAxisPixel, x.AspectRatio, x.OrientationDegrees,
                x.Circularity, x.Solidity, x.MeanGv, x.MinGv, x.MaxGv, x.StdDevGv, x.TouchesBorder,
                x.BoundingBoxX, x.BoundingBoxY, x.BoundingBoxWidth, x.BoundingBoxHeight
            };
            await writer.WriteLineAsync(string.Join(",", values.Select(Csv)));
        }
    }

    private static async Task WriteReportAsync(string path, SourceImageInfo info, AnalysisSettings settings,
        AnalysisResult result, CancellationToken token)
    {
        var rows = new StringBuilder();
        foreach (var x in result.Objects)
            rows.Append($"<tr><td>{x.ObjectId}</td><td>{x.FinalAccepted}</td><td>{x.AreaPixel2:F2}</td><td>{x.EquivalentDiameterPixel:F2}</td><td>{x.Circularity:F3}</td><td>{x.MeanGv:F1}</td><td>{WebUtility.HtmlEncode(x.RejectionSummary)}</td></tr>");
        var histogram = CreateHistogramSvg(ParticleStatistics.Histogram(
            result.Objects.Where(x => x.FinalAccepted).Select(x => x.EquivalentDiameterPixel)));
        var html = $$"""
        <!doctype html><html lang="ko"><head><meta charset="utf-8"><title>SEM Particle Analysis</title>
        <style>body{font:14px "Segoe UI",sans-serif;color:#25292d;margin:32px;max-width:1200px}h1{font-size:24px}h2{margin-top:28px;font-size:17px;border-bottom:1px solid #ddd;padding-bottom:8px}.meta{color:#687078}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}.grid img{width:100%;border:1px solid #ddd}table{border-collapse:collapse;width:100%;font-size:12px}th,td{padding:7px;border-bottom:1px solid #e3e3e3;text-align:right}th:first-child,td:first-child{text-align:left}</style></head>
        <body><h1>SEM Particle Analysis Report</h1>
        <p class="meta">{{WebUtility.HtmlEncode(info.FileName)}} · {{DateTime.Now:yyyy-MM-dd HH:mm:ss}} · SHA-256 {{info.Sha256}}</p>
        <div class="grid"><div><h2>Original</h2><img src="../images/original.png"></div><div><h2>Detection overlay</h2><img src="../images/detection_overlay.png"></div></div>
        <h2>Applied conditions</h2><p>Threshold: {{settings.ThresholdMode}}, GV {{settings.MinimumGv}}–{{settings.MaximumGv}} · ROI {{settings.Roi.X}}, {{settings.Roi.Y}}, {{settings.Roi.Width}} × {{settings.Roi.Height}} px · Border: {{settings.BorderRule}}</p>
        <h2>Summary</h2><p>Segmented {{result.Summary.SegmentedCount:N0}} · Accepted {{result.Summary.AcceptedCount:N0}} · Rejected {{result.Summary.RejectedCount:N0}} · D10/D50/D90 {{result.Summary.D10Pixel:F2}} / {{result.Summary.D50Pixel:F2}} / {{result.Summary.D90Pixel:F2}} px · Area fraction {{result.Summary.AreaFraction:P2}} · Processing {{result.Summary.ProcessingTime.TotalMilliseconds:N0}} ms</p>
        <h2>Equivalent diameter distribution</h2>{{histogram}}
        <h2>Objects</h2><table><thead><tr><th>ID</th><th>Accepted</th><th>Area px²</th><th>Eq. diameter px</th><th>Circularity</th><th>Mean GV</th><th>Rejected by</th></tr></thead><tbody>{{rows}}</tbody></table>
        </body></html>
        """;
        await File.WriteAllTextAsync(path, html, new UTF8Encoding(true), token);
    }

    private static string CreateHistogramSvg(IReadOnlyList<HistogramBin> bins)
    {
        if (bins.Count == 0) return "<p class=\"meta\">No accepted objects.</p>";
        const int width = 760;
        const int height = 220;
        const int margin = 34;
        var maximum = Math.Max(1, bins.Max(x => x.Count));
        var slot = (double)(width - margin * 2) / bins.Count;
        var svg = new StringBuilder($"<svg viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"Equivalent diameter histogram\">");
        svg.Append($"<line x1=\"{margin}\" y1=\"10\" x2=\"{margin}\" y2=\"{height - margin}\" stroke=\"#777\"/><line x1=\"{margin}\" y1=\"{height - margin}\" x2=\"{width - margin}\" y2=\"{height - margin}\" stroke=\"#777\"/>");
        for (var i = 0; i < bins.Count; i++)
        {
            var barHeight = (height - margin - 14d) * bins[i].Count / maximum;
            var x = margin + i * slot + 1;
            var y = height - margin - barHeight;
            svg.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{x:F2}\" y=\"{y:F2}\" width=\"{Math.Max(1, slot - 2):F2}\" height=\"{barHeight:F2}\" fill=\"#5084AE\"/>");
        }
        svg.Append($"<text x=\"{margin}\" y=\"{height - 8}\" font-size=\"11\">{bins[0].Minimum:F2}</text>");
        svg.Append($"<text x=\"{width - margin - 30}\" y=\"{height - 8}\" font-size=\"11\">{bins[^1].Maximum:F2}</text></svg>");
        return svg.ToString();
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken token)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, token);
    }

    private static string Csv(object? value)
    {
        var text = value switch
        {
            null => "",
            IFormattable valueFormattable => valueFormattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };
        return text.ContainsAny(',', '"', '\r', '\n') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static string UniqueDirectory(string candidate)
    {
        if (!Directory.Exists(candidate)) return candidate;
        for (var i = 2; ; i++)
        {
            var next = $"{candidate}_{i}";
            if (!Directory.Exists(next)) return next;
        }
    }

    private static Mat CreateLabeledOverlay(AnalysisResult result)
    {
        var labeled = result.Overlay.Clone();
        foreach (var item in result.Objects)
        {
            var color = item.FinalAccepted ? new Scalar(70, 230, 105) : new Scalar(50, 95, 255);
            var point = new Point(item.BoundingBoxX, Math.Max(12, item.BoundingBoxY - 4));
            Cv2.PutText(labeled, item.ObjectId.ToString(CultureInfo.InvariantCulture), point,
                HersheyFonts.HersheySimplex, .42, new Scalar(15, 15, 15), 3, LineTypes.AntiAlias);
            Cv2.PutText(labeled, item.ObjectId.ToString(CultureInfo.InvariantCulture), point,
                HersheyFonts.HersheySimplex, .42, color, 1, LineTypes.AntiAlias);
        }
        return labeled;
    }
}

internal static class StringExtensions
{
    public static bool ContainsAny(this string value, params char[] characters) => characters.Any(value.Contains);
}
