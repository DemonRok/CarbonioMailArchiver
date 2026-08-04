using CarbonioMailArchiver.Infrastructure.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class SevenZipArchiveExportServiceTests
{
  [Fact]
  public async Task CreateSevenZipAsync_CreatesArchiveWithRelativeEntries()
  {
    var root = Path.Combine(Path.GetTempPath(), $"CarbonioMailArchiverTests-{Guid.NewGuid():N}");
    var source = Path.Combine(root, "source");
    var nested = Path.Combine(source, "Inbox", "APC");
    Directory.CreateDirectory(nested);
    await File.WriteAllTextAsync(Path.Combine(source, "root.eml"), "root");
    await File.WriteAllTextAsync(Path.Combine(nested, "nested.eml"), "nested");

    var archivePath = Path.Combine(root, "mail.7z");
    var service = new SevenZipArchiveExportService();

    try
    {
      var result = await service.CreateSevenZipAsync(source, archivePath, 5, null, CancellationToken.None);

      Assert.Equal(Path.GetFullPath(archivePath), result);
      Assert.True(File.Exists(archivePath));
      var header = await File.ReadAllBytesAsync(archivePath);
      Assert.True(header.Length > 6);
      Assert.Equal([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], header.Take(6).Select(value => (int)value).ToArray());
    }
    finally
    {
      if (Directory.Exists(root))
      {
        Directory.Delete(root, recursive: true);
      }
    }
  }
}
