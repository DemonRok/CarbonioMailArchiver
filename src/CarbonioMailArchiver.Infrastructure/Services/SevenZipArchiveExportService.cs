using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class SevenZipArchiveExportService : IArchiveExportService
{
  public Task<string> CreateSevenZipAsync(
    string sourceDirectory,
    string archivePath,
    int compressionLevel,
    IProgress<ArchiveExportProgress>? progress,
    CancellationToken cancellationToken)
  {
    return Task.Run(
      () =>
      {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
          throw new DirectoryNotFoundException($"Cartella sorgente non trovata: {sourceDirectory}");
        }

        var targetDirectory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
          Directory.CreateDirectory(targetDirectory);
        }

        var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
        var fullArchivePath = Path.GetFullPath(archivePath);
        if (File.Exists(fullArchivePath))
        {
          File.Delete(fullArchivePath);
        }

        var files = Directory.EnumerateFiles(fullSourceDirectory, "*", SearchOption.AllDirectories).ToArray();
        using var stream = File.Create(fullArchivePath);
        var options = new SevenZipWriterOptions(CompressionType.LZMA)
        {
          CompressionLevel = Math.Clamp(compressionLevel, 0, 9)
        };
        using var writer = WriterFactory.OpenWriter(stream, ArchiveType.SevenZip, options);
        var completedCount = 0;
        foreach (var filePath in files)
        {
          cancellationToken.ThrowIfCancellationRequested();
          var entryName = Path.GetRelativePath(fullSourceDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
          completedCount++;
          progress?.Report(new ArchiveExportProgress(completedCount, files.Length, entryName));
          writer.Write(entryName, filePath);
        }

        return fullArchivePath;
      },
      cancellationToken);
  }
}
