using System.Text.Json;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public sealed class AnalysisPresetService : IAnalysisPresetService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task SaveAsync(string path, AnalysisSettings settings, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
    }

    public async Task<AnalysisSettings> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AnalysisSettings>(stream, Options, cancellationToken)
            ?? throw new InvalidDataException("분석 설정 파일의 형식이 올바르지 않습니다.");
    }
}
