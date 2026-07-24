using OpenCvSharp;

namespace SemParticleAnalyzer.Models;

public sealed class AnalysisResult : IDisposable
{
    public required IReadOnlyList<ParticleMeasurement> Objects { get; init; }
    public required AnalysisSummary Summary { get; init; }
    public required Mat Preprocessed { get; init; }
    public required Mat BinaryMask { get; init; }
    public required Mat Overlay { get; init; }
    public void Dispose()
    {
        Preprocessed.Dispose();
        BinaryMask.Dispose();
        Overlay.Dispose();
    }
}
