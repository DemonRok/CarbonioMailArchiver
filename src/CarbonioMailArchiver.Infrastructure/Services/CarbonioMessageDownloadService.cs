using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioMessageDownloadService(ILogger<CarbonioMessageDownloadService> logger) : IMessageDownloadService
{
  private const int SearchPageSize = 100;
  private const int CopyBufferSize = 81920;

  public async Task<MailDownloadResult> DownloadFolderTreeAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder rootFolder,
    IReadOnlyList<MailFolder> foldersToDownload,
    string downloadRootDirectory,
    int speedLimitKbps,
    IProgress<MailDownloadProgress>? progress,
    CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      return new MailDownloadResult(false, validationError, 0, string.Empty);
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      logger.LogWarning("Login Carbonio Auth fallito per download EML {Account}: {Reason}", settings.Email, loginError);
      return new MailDownloadResult(false, loginError, 0, string.Empty);
    }

    var targetDirectory = Path.Combine(downloadRootDirectory, SanitizePathSegment(settings.Email));
    var rootLocalDirectory = BuildLocalFolderDirectory(targetDirectory, rootFolder.AbsolutePath, rootFolder.AbsolutePath);
    if (Directory.Exists(rootLocalDirectory))
    {
      Directory.Delete(rootLocalDirectory, recursive: true);
    }

    var legacySelectedRootDirectory = BuildLegacyLocalFolderDirectory(targetDirectory, rootFolder.AbsolutePath);
    if (!string.Equals(legacySelectedRootDirectory, rootLocalDirectory, StringComparison.OrdinalIgnoreCase)
      && Directory.Exists(legacySelectedRootDirectory))
    {
      Directory.Delete(legacySelectedRootDirectory, recursive: true);
    }

    var legacyArchiveDirectory = Path.Combine(targetDirectory, "Archive");
    if (rootFolder.AbsolutePath.StartsWith("/Archive", StringComparison.OrdinalIgnoreCase)
      && Directory.Exists(legacyArchiveDirectory))
    {
      Directory.Delete(legacyArchiveDirectory, recursive: true);
    }

    var folders = foldersToDownload
      .OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase)
      .ToArray();
    var messagesByFolder = new Dictionary<string, IReadOnlyList<MailMessageSummary>>(StringComparer.Ordinal);
    var totalCount = 0;
    var operationStopwatch = Stopwatch.StartNew();
    long totalBytesDownloaded = 0;

    foreach (var folder in folders)
    {
      cancellationToken.ThrowIfCancellationRequested();
      progress?.Report(new MailDownloadProgress(folder.AbsolutePath, "Conteggio messaggi...", totalCount, 0, totalBytesDownloaded, operationStopwatch.Elapsed));
      var messages = await SearchAllMessagesAsync(client, folder, cancellationToken);
      messagesByFolder[folder.Id] = messages;
      totalCount += messages.Count;
    }

    var downloadedCount = 0;
    foreach (var folder in folders)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var folderDirectory = BuildLocalFolderDirectory(targetDirectory, folder.AbsolutePath, rootFolder.AbsolutePath);
      Directory.CreateDirectory(folderDirectory);

      foreach (var message in messagesByFolder[folder.Id])
      {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = BuildMessageFileName(message);
        var filePath = EnsureUniqueFilePath(Path.Combine(folderDirectory, fileName));
        progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), downloadedCount, totalCount, totalBytesDownloaded, operationStopwatch.Elapsed));

        using var response = await client.GetRawMessageAsync(message.Id, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
          var statusMessage = $"Download EML fallito per messaggio {message.Id}: HTTP {(int)response.StatusCode}.";
          logger.LogWarning("{Message}", statusMessage);
          return new MailDownloadResult(false, statusMessage, downloadedCount, targetDirectory);
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(filePath);
        await CopyToAsync(
          source,
          destination,
          speedLimitKbps,
          bytesCopied =>
          {
            totalBytesDownloaded += bytesCopied;
            progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), downloadedCount, totalCount, totalBytesDownloaded, operationStopwatch.Elapsed));
          },
          cancellationToken);
        ApplyMessageFileTimestamps(filePath, message.Date);
        downloadedCount++;
        progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), downloadedCount, totalCount, totalBytesDownloaded, operationStopwatch.Elapsed));
      }
    }

    logger.LogInformation(
      "Download EML completato per {Account}. Cartella radice: {RootFolder}. Messaggi scaricati: {Count}.",
      settings.Email,
      rootFolder.AbsolutePath,
      downloadedCount);
    return new MailDownloadResult(true, $"Download EML completato. Messaggi scaricati: {downloadedCount}.", downloadedCount, targetDirectory);
  }

  internal static string BuildLocalFolderDirectory(string accountDirectory, string folderPath, string rootFolderPath)
  {
    var relativePath = BuildRelativeDownloadPath(folderPath, rootFolderPath);
    var relativeParts = relativePath
      .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(SanitizePathSegment)
      .Where(part => !string.IsNullOrWhiteSpace(part))
      .ToArray();
    return relativeParts.Length == 0
      ? accountDirectory
      : Path.Combine([accountDirectory, .. relativeParts]);
  }

  internal static string BuildRelativeDownloadPath(string folderPath, string rootFolderPath)
  {
    var normalizedFolderPath = NormalizeFolderPath(folderPath);
    var normalizedRootFolderPath = NormalizeFolderPath(rootFolderPath);

    if (string.Equals(normalizedFolderPath, normalizedRootFolderPath, StringComparison.OrdinalIgnoreCase))
    {
      return string.Empty;
    }

    var rootPrefix = normalizedRootFolderPath.TrimEnd('/') + "/";
    if (normalizedFolderPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return normalizedFolderPath[rootPrefix.Length..];
    }

    if (normalizedFolderPath.StartsWith("/Archive/", StringComparison.OrdinalIgnoreCase))
    {
      return normalizedFolderPath["/Archive/".Length..];
    }

    return normalizedFolderPath.TrimStart('/');
  }

  private static string BuildLegacyLocalFolderDirectory(string accountDirectory, string folderPath)
  {
    var relativeParts = NormalizeFolderPath(folderPath)
      .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(SanitizePathSegment)
      .Where(part => !string.IsNullOrWhiteSpace(part))
      .ToArray();
    return relativeParts.Length == 0
      ? accountDirectory
      : Path.Combine([accountDirectory, .. relativeParts]);
  }

  private static string NormalizeFolderPath(string folderPath)
  {
    var normalized = "/" + folderPath.Trim().Trim('/');
    return normalized == "/" ? string.Empty : normalized;
  }

  internal static string SanitizePathSegment(string value)
  {
    var invalidChars = Path.GetInvalidFileNameChars();
    var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
    return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
  }

  internal static DateTime? ToLocalFileTimestamp(DateTimeOffset? messageDate)
  {
    return messageDate?.LocalDateTime;
  }

  private static async Task<IReadOnlyList<MailMessageSummary>> SearchAllMessagesAsync(
    CarbonioWebClient client,
    MailFolder folder,
    CancellationToken cancellationToken)
  {
    var messages = new List<MailMessageSummary>();
    var knownIds = new HashSet<string>(StringComparer.Ordinal);
    var offset = 0;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      using var response = await client.PostSearchAsync($"inid:{folder.Id}", SearchPageSize, offset, cancellationToken);
      var content = await response.Content.ReadAsStringAsync(cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        throw new InvalidOperationException($"SearchRequest download fallita per {folder.AbsolutePath}: HTTP {(int)response.StatusCode}.");
      }

      var page = ParseSearchResult(content);
      foreach (var message in page.Messages)
      {
        if (!string.IsNullOrWhiteSpace(message.Id) && knownIds.Add(message.Id))
        {
          messages.Add(message);
        }
      }

      if (!page.HasMore || page.Messages.Count < SearchPageSize)
      {
        return messages;
      }

      offset += SearchPageSize;
    }
  }

  private static void ApplyMessageFileTimestamps(string filePath, DateTimeOffset? messageDate)
  {
    var timestamp = ToLocalFileTimestamp(messageDate);
    if (timestamp is null)
    {
      return;
    }

    File.SetCreationTime(filePath, timestamp.Value);
    File.SetLastWriteTime(filePath, timestamp.Value);
    File.SetLastAccessTime(filePath, timestamp.Value);
  }

  private static MailSearchResult ParseSearchResult(string json)
  {
    using var document = JsonDocument.Parse(json);
    if (!TryFindProperty(document.RootElement, "SearchResponse", out var searchResponse))
    {
      throw new JsonException("Risposta SearchRequest senza SearchResponse.");
    }

    var messages = new List<MailMessageSummary>();
    if (TryFindProperty(searchResponse, "m", out var messageElement))
    {
      if (messageElement.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in messageElement.EnumerateArray())
        {
          messages.Add(ParseMessage(item));
        }
      }
      else if (messageElement.ValueKind == JsonValueKind.Object)
      {
        messages.Add(ParseMessage(messageElement));
      }
    }

    return new MailSearchResult(messages, ReadInt(searchResponse, "num"), ReadBool(searchResponse, "more"));
  }

  private static MailMessageSummary ParseMessage(JsonElement item)
  {
    var id = ReadString(item, "id") ?? string.Empty;
    var folderId = ReadString(item, "l") ?? string.Empty;
    var subject = ReadString(item, "su") ?? string.Empty;
    var date = ReadUnixMilliseconds(item, "d");
    var size = ReadLong(item, "s");
    return new MailMessageSummary(id, date, string.Empty, subject, size, folderId);
  }

  private static async Task CopyToAsync(
    Stream source,
    Stream destination,
    int speedLimitKbps,
    Action<int> reportBytesCopied,
    CancellationToken cancellationToken)
  {
    var buffer = new byte[CopyBufferSize];
    if (speedLimitKbps <= 0)
    {
      while (true)
      {
        var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (read == 0)
        {
          return;
        }

        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        reportBytesCopied(read);
      }
    }

    var bytesPerSecond = speedLimitKbps * 1024d;
    var stopwatch = Stopwatch.StartNew();
    long copiedBytes = 0;

    while (true)
    {
      var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
      if (read == 0)
      {
        return;
      }

      await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
      copiedBytes += read;
      reportBytesCopied(read);
      var expectedElapsed = TimeSpan.FromSeconds(copiedBytes / bytesPerSecond);
      var delay = expectedElapsed - stopwatch.Elapsed;
      if (delay > TimeSpan.FromMilliseconds(20))
      {
        await Task.Delay(delay, cancellationToken);
      }
    }
  }

  private static string BuildMessageFileName(MailMessageSummary message)
  {
    var datePrefix = message.Date?.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) ?? "nodate";
    return SanitizePathSegment($"{datePrefix}_{message.Id}.eml");
  }

  private static string EnsureUniqueFilePath(string filePath)
  {
    if (!File.Exists(filePath))
    {
      return filePath;
    }

    var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
    var extension = Path.GetExtension(filePath);
    for (var index = 1; index < int.MaxValue; index++)
    {
      var candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_{index}{extension}");
      if (!File.Exists(candidate))
      {
        return candidate;
      }
    }

    throw new IOException($"Impossibile creare un nome file univoco per {filePath}.");
  }

  private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
  {
    if (element.ValueKind == JsonValueKind.Object)
    {
      foreach (var property in element.EnumerateObject())
      {
        if (property.NameEquals(propertyName))
        {
          value = property.Value;
          return true;
        }

        if (TryFindProperty(property.Value, propertyName, out value))
        {
          return true;
        }
      }
    }

    if (element.ValueKind == JsonValueKind.Array)
    {
      foreach (var item in element.EnumerateArray())
      {
        if (TryFindProperty(item, propertyName, out value))
        {
          return true;
        }
      }
    }

    value = default;
    return false;
  }

  private static string? ReadString(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
      return null;
    }

    return property.ValueKind switch
    {
      JsonValueKind.String => property.GetString(),
      JsonValueKind.Number => property.GetRawText(),
      _ => null
    };
  }

  private static int? ReadInt(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
      return null;
    }

    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
    {
      return number;
    }

    if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
    {
      return number;
    }

    return null;
  }

  private static long? ReadLong(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
      return null;
    }

    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
    {
      return number;
    }

    if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
    {
      return number;
    }

    return null;
  }

  private static bool ReadBool(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
      return false;
    }

    return property.ValueKind switch
    {
      JsonValueKind.True => true,
      JsonValueKind.Number => property.TryGetInt32(out var number) && number != 0,
      JsonValueKind.String => property.GetString() is "1" or "true" or "TRUE",
      _ => false
    };
  }

  private static DateTimeOffset? ReadUnixMilliseconds(JsonElement element, string propertyName)
  {
    var value = ReadLong(element, propertyName);
    return value is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(value.Value);
  }
}
