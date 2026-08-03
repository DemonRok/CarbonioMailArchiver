using CarbonioMailArchiver.Infrastructure.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class CarbonioMessageDownloadServiceTests
{
  [Fact]
  public void BuildLocalFolderDirectory_RecreatesFolderPathUnderAccountDirectory()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Inbox/ANIMALI_UDA/Esempio", "/Inbox/ANIMALI_UDA");

    Assert.EndsWith(Path.Combine("mauro@example.test", "Inbox", "ANIMALI_UDA", "Esempio"), result);
  }

  [Fact]
  public void BuildLocalFolderDirectory_SkipsArchiveSegmentWhenArchiveIsDownloadRoot()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Archive/Inbox/ANIMALI_UDA/Esempio", "/Archive");

    Assert.EndsWith(Path.Combine("mauro@example.test", "Inbox", "ANIMALI_UDA", "Esempio"), result);
  }

  [Theory]
  [InlineData("Archivio:2026", "Archivio_2026")]
  [InlineData("  ", "_")]
  public void SanitizePathSegment_ReplacesInvalidOrEmptyNames(string value, string expected)
  {
    Assert.Equal(expected, CarbonioMessageDownloadService.SanitizePathSegment(value));
  }
}
