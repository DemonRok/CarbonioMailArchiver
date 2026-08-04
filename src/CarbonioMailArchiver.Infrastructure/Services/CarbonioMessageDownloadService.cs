using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioMessageDownloadService(ILogger<CarbonioMessageDownloadService> logger) : IMessageDownloadService
{
  private const int CopyBufferSize = 81920;

  public async Task<MailDownloadResult> DownloadFolderTreeAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder rootFolder,
    IReadOnlyList<MailFolder> foldersToDownload,
    string downloadRootDirectory,
    int batchSize,
    int speedLimitKbps,
    int retryCount,
    int retryDelaySeconds,
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
    var searchPageSize = Math.Clamp(batchSize, 10, 500);
    var folders = foldersToDownload
      .OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase)
      .ToArray();
    var messagesByFolder = new Dictionary<string, IReadOnlyList<MailMessageSummary>>(StringComparer.Ordinal);
    var totalCount = 0;
    var operationStopwatch = Stopwatch.StartNew();
    long totalBytesDownloaded = 0;
    var skippedCount = 0;
    var downloadedThisSessionCount = 0;

    try
    {
      foreach (var folder in folders)
      {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new MailDownloadProgress(folder.AbsolutePath, "Conteggio messaggi...", 0, totalCount, skippedCount, downloadedThisSessionCount, totalBytesDownloaded, operationStopwatch.Elapsed));
        var messages = await SearchAllMessagesAsync(client, folder, searchPageSize, cancellationToken);
        messagesByFolder[folder.Id] = messages;
        totalCount += messages.Count;
      }

      var completedCount = 0;
      foreach (var folder in folders)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var folderDirectory = BuildLocalFolderDirectory(targetDirectory, folder.AbsolutePath, rootFolder.AbsolutePath);
        Directory.CreateDirectory(folderDirectory);

        foreach (var message in messagesByFolder[folder.Id])
        {
          cancellationToken.ThrowIfCancellationRequested();
          var fileName = BuildMessageFileName(message);
          var filePath = Path.Combine(folderDirectory, fileName);
          if (File.Exists(filePath))
          {
            await RepairMessageFileTimestampAsync(filePath, message.Date, cancellationToken);
            skippedCount++;
            completedCount++;
            progress?.Report(new MailDownloadProgress(folder.AbsolutePath, $"{fileName} gia' presente, salto.", completedCount, totalCount, skippedCount, downloadedThisSessionCount, totalBytesDownloaded, operationStopwatch.Elapsed));
            continue;
          }

          progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), completedCount, totalCount, skippedCount, downloadedThisSessionCount, totalBytesDownloaded, operationStopwatch.Elapsed));
          var download = await DownloadMessageWithRetryAsync(
            client,
            message,
            folder.AbsolutePath,
            filePath,
            speedLimitKbps,
            retryCount,
            retryDelaySeconds,
            bytesCopied =>
            {
              totalBytesDownloaded += bytesCopied;
              progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), completedCount, totalCount, skippedCount, downloadedThisSessionCount, totalBytesDownloaded, operationStopwatch.Elapsed));
            },
            cancellationToken);
          if (!download.IsSuccess)
          {
            var failedVerification = await VerifyDownloadedMessagesAsync(targetDirectory, rootFolder, folders, messagesByFolder, false, null, cancellationToken);
            var failedMessage = AppendVerificationSummary(download.Message, failedVerification);
            return new MailDownloadResult(false, failedMessage, completedCount, targetDirectory, failedVerification.ExpectedCount, failedVerification.PresentCount, failedVerification.MissingCount);
          }

          completedCount++;
          downloadedThisSessionCount++;
          progress?.Report(new MailDownloadProgress(folder.AbsolutePath, Path.GetFileName(filePath), completedCount, totalCount, skippedCount, downloadedThisSessionCount, totalBytesDownloaded, operationStopwatch.Elapsed));
        }
      }

      var verification = await VerifyDownloadedMessagesAsync(targetDirectory, rootFolder, folders, messagesByFolder, false, null, cancellationToken);
      logger.LogInformation(
        "Download EML completato per {Account}. Cartella radice: {RootFolder}. Messaggi completati: {Count}. Saltati per resume: {SkippedCount}. Verifica: presenti {PresentCount}/{ExpectedCount}, mancanti {MissingCount}.",
        settings.Email,
        rootFolder.AbsolutePath,
        completedCount,
        skippedCount,
        verification.PresentCount,
        verification.ExpectedCount,
        verification.MissingCount);
      var skippedMessage = skippedCount == 0 ? string.Empty : $" Gia' presenti saltati: {skippedCount}.";
      var successMessage = AppendVerificationSummary($"Download EML completato. Messaggi completati: {completedCount}.{skippedMessage}", verification);
      return new MailDownloadResult(verification.MissingCount == 0, successMessage, completedCount, targetDirectory, verification.ExpectedCount, verification.PresentCount, verification.MissingCount);
    }
    catch (OperationCanceledException)
    {
      var verification = await VerifyDownloadedMessagesAsync(targetDirectory, rootFolder, folders, messagesByFolder, false, null, CancellationToken.None);
      var cancelMessage = AppendVerificationSummary("Download EML annullato dall'utente.", verification);
      logger.LogWarning(
        "Download EML annullato per {Account}. Verifica parziale: presenti {PresentCount}/{ExpectedCount}, mancanti {MissingCount}.",
        settings.Email,
        verification.PresentCount,
        verification.ExpectedCount,
        verification.MissingCount);
      return new MailDownloadResult(false, cancelMessage, verification.PresentCount, targetDirectory, verification.ExpectedCount, verification.PresentCount, verification.MissingCount);
    }
  }

  public async Task<MailDownloadResult> VerifyFolderTreeAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder rootFolder,
    IReadOnlyList<MailFolder> foldersToVerify,
    string downloadRootDirectory,
    int batchSize,
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
      logger.LogWarning("Login Carbonio Auth fallito per verifica EML {Account}: {Reason}", settings.Email, loginError);
      return new MailDownloadResult(false, loginError, 0, string.Empty);
    }

    var targetDirectory = Path.Combine(downloadRootDirectory, SanitizePathSegment(settings.Email));
    var searchPageSize = Math.Clamp(batchSize, 10, 500);
    var folders = foldersToVerify
      .OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase)
      .ToArray();
    var messagesByFolder = new Dictionary<string, IReadOnlyList<MailMessageSummary>>(StringComparer.Ordinal);
    var totalCount = 0;
    var operationStopwatch = Stopwatch.StartNew();

    foreach (var folder in folders)
    {
      cancellationToken.ThrowIfCancellationRequested();
      progress?.Report(new MailDownloadProgress(folder.AbsolutePath, "Verifica messaggi...", 0, totalCount, 0, 0, 0, operationStopwatch.Elapsed));
      var messages = await SearchAllMessagesAsync(client, folder, searchPageSize, cancellationToken);
      messagesByFolder[folder.Id] = messages;
      totalCount += messages.Count;
    }

    var verification = await VerifyDownloadedMessagesAsync(targetDirectory, rootFolder, folders, messagesByFolder, true, progress, cancellationToken);
    var message = AppendVerificationSummary("Verifica EML completata.", verification);
    logger.LogInformation(
      "Verifica EML completata per {Account}. Cartella radice: {RootFolder}. Presenti {PresentCount}/{ExpectedCount}, mancanti {MissingCount}.",
      settings.Email,
      rootFolder.AbsolutePath,
      verification.PresentCount,
      verification.ExpectedCount,
      verification.MissingCount);
    progress?.Report(new MailDownloadProgress(rootFolder.AbsolutePath, "Verifica completata.", verification.PresentCount, verification.ExpectedCount, 0, 0, 0, operationStopwatch.Elapsed));
    return new MailDownloadResult(verification.MissingCount == 0, message, verification.PresentCount, targetDirectory, verification.ExpectedCount, verification.PresentCount, verification.MissingCount);
  }

  private sealed record DownloadVerification(int ExpectedCount, int PresentCount, int MissingCount);

  private static Task<DownloadVerification> VerifyDownloadedMessagesAsync(
    string targetDirectory,
    MailFolder rootFolder,
    IReadOnlyList<MailFolder> folders,
    IReadOnlyDictionary<string, IReadOnlyList<MailMessageSummary>> messagesByFolder,
    bool repairTimestamps,
    IProgress<MailDownloadProgress>? progress,
    CancellationToken cancellationToken)
  {
    return Task.Run(
      () => VerifyDownloadedMessages(targetDirectory, rootFolder, folders, messagesByFolder, repairTimestamps, progress, cancellationToken),
      cancellationToken);
  }

  private static DownloadVerification VerifyDownloadedMessages(
    string targetDirectory,
    MailFolder rootFolder,
    IReadOnlyList<MailFolder> folders,
    IReadOnlyDictionary<string, IReadOnlyList<MailMessageSummary>> messagesByFolder,
    bool repairTimestamps,
    IProgress<MailDownloadProgress>? progress,
    CancellationToken cancellationToken)
  {
    var expectedCount = 0;
    var presentCount = 0;
    var processedCount = 0;
    foreach (var folder in folders)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!messagesByFolder.TryGetValue(folder.Id, out var messages))
      {
        continue;
      }

      var folderDirectory = BuildLocalFolderDirectory(targetDirectory, folder.AbsolutePath, rootFolder.AbsolutePath);
      foreach (var message in messages)
      {
        cancellationToken.ThrowIfCancellationRequested();
        expectedCount++;
        processedCount++;
        var filePath = Path.Combine(folderDirectory, BuildMessageFileName(message));
        if (File.Exists(filePath))
        {
          if (repairTimestamps)
          {
            ApplyMessageFileTimestamps(filePath, message.Date);
          }

          presentCount++;
        }

        if (processedCount % 250 == 0)
        {
          progress?.Report(new MailDownloadProgress(folder.AbsolutePath, "Verifica file locali...", presentCount, expectedCount, 0, 0, 0, TimeSpan.Zero));
        }
      }
    }

    return new DownloadVerification(expectedCount, presentCount, Math.Max(expectedCount - presentCount, 0));
  }

  private static Task RepairMessageFileTimestampAsync(string filePath, DateTimeOffset? messageDate, CancellationToken cancellationToken)
  {
    return Task.Run(
      () =>
      {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyMessageFileTimestamps(filePath, messageDate);
      },
      cancellationToken);
  }

  private static string AppendVerificationSummary(string message, DownloadVerification verification)
  {
    if (verification.ExpectedCount == 0)
    {
      return $"{message} Verifica: nessun messaggio atteso rilevato.";
    }

    var result = verification.MissingCount == 0 ? "OK" : $"{verification.MissingCount} mancanti";
    return $"{message} Verifica EML: presenti {verification.PresentCount}/{verification.ExpectedCount}, {result}.";
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

  internal static string BuildTemporaryFilePath(string filePath)
  {
    return $"{filePath}.download";
  }

  private static async Task<IReadOnlyList<MailMessageSummary>> SearchAllMessagesAsync(
    CarbonioWebClient client,
    MailFolder folder,
    int searchPageSize,
    CancellationToken cancellationToken)
  {
    var messages = new List<MailMessageSummary>();
    var knownIds = new HashSet<string>(StringComparer.Ordinal);
    var offset = 0;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      using var response = await client.PostSearchAsync($"inid:{folder.Id}", searchPageSize, offset, cancellationToken);
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

      if (!page.HasMore || page.Messages.Count < searchPageSize)
      {
        return messages;
      }

      offset += searchPageSize;
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

  private static void DeleteTemporaryFileIfExists(string tempFilePath)
  {
    if (File.Exists(tempFilePath))
    {
      File.Delete(tempFilePath);
    }
  }

  private async Task<(bool IsSuccess, string Message)> DownloadMessageWithRetryAsync(
    CarbonioWebClient client,
    MailMessageSummary message,
    string folderPath,
    string filePath,
    int speedLimitKbps,
    int retryCount,
    int retryDelaySeconds,
    Action<int> reportBytesCopied,
    CancellationToken cancellationToken)
  {
    var tempFilePath = BuildTemporaryFilePath(filePath);
    var maxAttempts = Math.Clamp(retryCount, 1, 10);
    var retryDelay = Math.Clamp(retryDelaySeconds, 1, 300);
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      try
      {
        DeleteTemporaryFileIfExists(tempFilePath);
        using var response = await client.GetRawMessageAsync(message.Id, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
          var statusMessage = $"Download EML fallito per messaggio {message.Id}: HTTP {(int)response.StatusCode}.";
          if (!IsTransientStatusCode((int)response.StatusCode) || attempt == maxAttempts)
          {
            logger.LogWarning("{Message} Cartella: {FolderPath}. Tentativo: {Attempt}/{MaxAttempts}.", statusMessage, folderPath, attempt, maxAttempts);
            return (false, statusMessage);
          }

          logger.LogWarning(
            "{Message} Cartella: {FolderPath}. Retry {Attempt}/{MaxAttempts}.",
            statusMessage,
            folderPath,
            attempt,
            maxAttempts);
          await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
          continue;
        }

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = File.Create(tempFilePath))
        {
          await CopyToAsync(source, destination, speedLimitKbps, reportBytesCopied, cancellationToken);
        }

        ApplyMessageFileTimestamps(tempFilePath, message.Date);
        File.Move(tempFilePath, filePath);
        ApplyMessageFileTimestamps(filePath, message.Date);
        return (true, string.Empty);
      }
      catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
      {
        DeleteTemporaryFileIfExists(tempFilePath);
        if (cancellationToken.IsCancellationRequested)
        {
          throw;
        }

        if (attempt == maxAttempts)
        {
          var errorMessage = $"Download EML fallito per messaggio {message.Id} dopo {maxAttempts} tentativi su {filePath}: {ex.Message}";
          logger.LogWarning(ex, "{Message} Cartella: {FolderPath}.", errorMessage, folderPath);
          return (false, errorMessage);
        }

        logger.LogWarning(
          ex,
          "Download EML fallito per messaggio {MessageId} in {FolderPath}. Retry {Attempt}/{MaxAttempts}.",
          message.Id,
          folderPath,
          attempt,
          maxAttempts);
        await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
      }
    }

    return (false, $"Download EML fallito per messaggio {message.Id}.");
  }

  private static bool IsTransientStatusCode(int statusCode)
  {
    return statusCode is 408 or 429 or >= 500;
  }

  private static Task DelayBeforeRetryAsync(int attempt, int retryDelaySeconds, CancellationToken cancellationToken)
  {
    return Task.Delay(TimeSpan.FromSeconds(attempt * retryDelaySeconds), cancellationToken);
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
