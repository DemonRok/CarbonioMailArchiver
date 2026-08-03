using CarbonioMailArchiver.Infrastructure.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class CarbonioMessageDownloadServiceTests
{
  [Fact]
  public void BuildLocalFolderDirectory_SkipsSelectedRootFolder()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Inbox/ANIMALI_UDA/Esempio", "/Inbox/ANIMALI_UDA");

    Assert.EndsWith(Path.Combine("mauro@example.test", "Esempio"), result);
  }

  [Fact]
  public void BuildLocalFolderDirectory_SavesArchiveContentsUnderAccountDirectory()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Archive/Inbox/ANIMALI_UDA/Esempio", "/Archive");

    Assert.EndsWith(Path.Combine("mauro@example.test", "Inbox", "ANIMALI_UDA", "Esempio"), result);
  }

  [Fact]
  public void BuildLocalFolderDirectory_SkipsArchiveSegmentWhenSelectedRootIsInsideArchive()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Archive/Inbox", "/Archive/Inbox");

    Assert.Equal(accountDirectory, result);
  }

  [Fact]
  public void BuildLocalFolderDirectory_SavesChildOfSelectedArchiveRootWithoutArchiveOrRoot()
  {
    var accountDirectory = Path.Combine("D:", "Downloads", "mauro@example.test");

    var result = CarbonioMessageDownloadService.BuildLocalFolderDirectory(accountDirectory, "/Archive/Inbox/APC", "/Archive/Inbox");

    Assert.EndsWith(Path.Combine("mauro@example.test", "APC"), result);
  }

  [Theory]
  [InlineData("Archivio:2026", "Archivio_2026")]
  [InlineData("  ", "_")]
  public void SanitizePathSegment_ReplacesInvalidOrEmptyNames(string value, string expected)
  {
    Assert.Equal(expected, CarbonioMessageDownloadService.SanitizePathSegment(value));
  }

  [Fact]
  public void ToLocalFileTimestamp_UsesMessageDateWhenAvailable()
  {
    var messageDate = new DateTimeOffset(2026, 7, 15, 10, 30, 0, TimeSpan.Zero);

    var result = CarbonioMessageDownloadService.ToLocalFileTimestamp(messageDate);

    Assert.Equal(messageDate.LocalDateTime, result);
  }

  [Fact]
  public void BuildTemporaryFilePath_AppendsDownloadExtension()
  {
    var filePath = Path.Combine("D:", "Downloads", "message.eml");

    var result = CarbonioMessageDownloadService.BuildTemporaryFilePath(filePath);

    Assert.Equal($"{filePath}.download", result);
  }
}
