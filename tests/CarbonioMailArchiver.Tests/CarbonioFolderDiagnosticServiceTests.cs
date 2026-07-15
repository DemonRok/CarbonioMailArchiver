using CarbonioMailArchiver.Infrastructure.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class CarbonioFolderDiagnosticServiceTests
{
  [Fact]
  public void ParseFolders_BuildsAbsolutePathsById()
  {
    const string json = """
      {
        "Body": {
          "GetFolderResponse": {
            "folder": [{
              "id": "1",
              "name": "USER_ROOT",
              "absFolderPath": "/",
              "folder": [
                { "id": "2", "name": "Inbox", "absFolderPath": "/Inbox" },
                {
                  "id": "10",
                  "name": "Archivio",
                  "absFolderPath": "/Inbox/Archivio",
                  "folder": { "id": "11", "name": "2024", "absFolderPath": "/Inbox/Archivio/2024" }
                }
              ]
            }]
          }
        }
      }
      """;

    var folders = CarbonioFolderDiagnosticService.ParseFolders(json);

    Assert.True(folders["2"].IsInbox);
    Assert.Equal("/Inbox/Archivio/2024", folders["11"].AbsolutePath);
  }
}
