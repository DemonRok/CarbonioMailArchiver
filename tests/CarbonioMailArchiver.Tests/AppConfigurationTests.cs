using CarbonioMailArchiver.Core.Models;
using CarbonioMailArchiver.Infrastructure.Configuration;

namespace CarbonioMailArchiver.Tests;

public sealed class AppConfigurationTests
{
  [Fact]
  public async Task SaveConnectionSettingsAsync_DoesNotPersistPassword()
  {
    var directory = Path.Combine(Path.GetTempPath(), "CarbonioMailArchiver.Tests", Guid.NewGuid().ToString("N"));
    var configuration = new AppConfiguration(directory);
    var settings = new CarbonioConnectionSettings
    {
      BaseUrl = "https://mail.example.test",
      SoapUrl = "https://mail.example.test/service/soap",
      Email = "user@example.test",
      LastSourceFolderId = "2",
      LastDestinationFolderId = "23747",
      UseArchiveDestination = true,
      IncludeSourceSubfolders = true,
      RememberCredentials = true
    };

    await configuration.SaveConnectionSettingsAsync(settings, CancellationToken.None);

    var json = await File.ReadAllTextAsync(Path.Combine(directory, "settings.json"));
    Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("user@example.test", json, StringComparison.Ordinal);
    Assert.Contains("\"LastSourceFolderId\": \"2\"", json, StringComparison.Ordinal);
    Assert.Contains("\"LastDestinationFolderId\": \"23747\"", json, StringComparison.Ordinal);
    Assert.Contains("\"UseArchiveDestination\": true", json, StringComparison.Ordinal);
    Assert.Contains("\"IncludeSourceSubfolders\": true", json, StringComparison.Ordinal);
  }
}
