using OpenCvSharp;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public interface IImageLoader
{
    Task<(Mat Image, SourceImageInfo Info)> LoadAsync(string path, CancellationToken cancellationToken);
}

public interface IAnalysisService
{
    Task<AnalysisResult> AnalyzeAsync(Mat source, AnalysisSettings settings, CancellationToken cancellationToken);
}

public interface IResultExportService
{
    Task<string> ExportAsync(string destinationRoot, Mat source, SourceImageInfo info,
        AnalysisSettings settings, AnalysisResult result, CancellationToken cancellationToken);
}

public interface IAnalysisPresetService
{
    Task SaveAsync(string path, AnalysisSettings settings, CancellationToken cancellationToken);
    Task<AnalysisSettings> LoadAsync(string path, CancellationToken cancellationToken);
}
