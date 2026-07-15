using System.Text.Json;

namespace CarbonioMailArchiver.Infrastructure.Configuration;

internal static class JsonOptions
{
  public static readonly JsonSerializerOptions Default = new()
  {
    WriteIndented = true
  };
}
