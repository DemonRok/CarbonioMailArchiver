using System.Text.Json;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Infrastructure.Configuration;

public sealed class AppConfiguration
{
  private readonly string _settingsPath;

  public AppConfiguration()
    : this(null)
  {
  }

  public AppConfiguration(string? applicationDirectory)
  {
    var directory = applicationDirectory
      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CarbonioMailArchiver");
    Directory.CreateDirectory(directory);
    _settingsPath = Path.Combine(directory, "settings.json");
  }

  public async Task<CarbonioConnectionSettings> LoadConnectionSettingsAsync(CancellationToken cancellationToken)
  {
    if (!File.Exists(_settingsPath))
    {
      return new CarbonioConnectionSettings();
    }

    await using var stream = File.OpenRead(_settingsPath);
    return await JsonSerializer.DeserializeAsync<CarbonioConnectionSettings>(stream, JsonOptions.Default, cancellationToken)
      ?? new CarbonioConnectionSettings();
  }

  public async Task SaveConnectionSettingsAsync(CarbonioConnectionSettings settings, CancellationToken cancellationToken)
  {
    await using var stream = File.Create(_settingsPath);
    await JsonSerializer.SerializeAsync(stream, settings, JsonOptions.Default, cancellationToken);
  }
}
