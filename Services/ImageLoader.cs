using System.Security.Cryptography;
using OpenCvSharp;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Services;

public sealed class ImageLoader : IImageLoader
{
    public async Task<(Mat Image, SourceImageInfo Info)> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("이미지 파일을 찾을 수 없습니다.", path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        var image = Cv2.ImRead(path, ImreadModes.Unchanged);
        if (image.Empty())
        {
            image.Dispose();
            throw new InvalidDataException("지원하지 않거나 손상된 이미지입니다.");
        }
        var file = new FileInfo(path);
        return (image, new SourceImageInfo
        {
            FilePath = file.FullName,
            FileName = file.Name,
            FileSize = file.Length,
            Sha256 = hash,
            Width = image.Width,
            Height = image.Height,
            Channels = image.Channels(),
            Depth = image.Depth().ToString()
        });
    }
}
