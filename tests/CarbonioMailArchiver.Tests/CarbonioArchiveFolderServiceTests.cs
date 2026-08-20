using CarbonioMailArchiver.Infrastructure.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class CarbonioArchiveFolderServiceTests
{
  [Theory]
  [InlineData("/Inbox/ANIMALI_UDA", "/Archive/Inbox/ANIMALI_UDA")]
  [InlineData("/Inbox/ANIMALI_UDA/Esempio", "/Archive/Inbox/ANIMALI_UDA/Esempio")]
  [InlineData("Inbox/ANIMALI_UDA", "/Archive/Inbox/ANIMALI_UDA")]
  public void BuildArchivePath_ReplicatesSourcePathUnderArchive(string sourcePath, string expectedPath)
  {
    Assert.Equal(expectedPath, CarbonioArchiveFolderService.BuildArchivePath(sourcePath));
  }

  [Theory]
  [InlineData("/Inbox/ANIMALI_UDA", "/Trash/Inbox/ANIMALI_UDA")]
  [InlineData("/Inbox/ANIMALI_UDA/Esempio", "/Trash/Inbox/ANIMALI_UDA/Esempio")]
  [InlineData("/", "/Trash")]
  public void BuildTrashPath_ReplicatesSourcePathUnderTrash(string sourcePath, string expectedPath)
  {
    Assert.Equal(expectedPath, CarbonioArchiveFolderService.BuildTrashPath(sourcePath));
  }
}
