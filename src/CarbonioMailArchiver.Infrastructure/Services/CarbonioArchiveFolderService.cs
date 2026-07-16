using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioArchiveFolderService(ILogger<CarbonioArchiveFolderService> logger) : IArchiveFolderService
{
  public async Task<ArchiveFolderEnsureResult> EnsureArchiveDestinationAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder sourceFolder,
    CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      return new ArchiveFolderEnsureResult(false, null, validationError, []);
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      return new ArchiveFolderEnsureResult(false, null, loginError, []);
    }

    IReadOnlyDictionary<string, MailFolder> foldersById;
    try
    {
      foldersById = await LoadFoldersAsync(client, settings.Email, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Errore HTTP durante preparazione Archivio per {Account}.", settings.Email);
      return new ArchiveFolderEnsureResult(false, null, $"Preparazione Archivio fallita: {ex.Message}", []);
    }
    catch (InvalidOperationException ex)
    {
      logger.LogWarning(ex, "Lettura cartelle fallita durante preparazione Archivio per {Account}.", settings.Email);
      return new ArchiveFolderEnsureResult(false, null, ex.Message, []);
    }
    if (!TryFindByPath(foldersById, "/Archive", out var archiveRoot))
    {
      return new ArchiveFolderEnsureResult(false, null, "Cartella /Archive non trovata. Verifica che Archivio sia attivo nella casella.", []);
    }

    var targetPath = BuildArchivePath(sourceFolder.AbsolutePath);
    if (string.Equals(sourceFolder.AbsolutePath, targetPath, StringComparison.OrdinalIgnoreCase))
    {
      return new ArchiveFolderEnsureResult(false, null, "La sorgente selezionata e' gia' dentro /Archive.", []);
    }

    if (TryFindByPath(foldersById, targetPath, out var existingTarget))
    {
      return new ArchiveFolderEnsureResult(true, existingTarget, $"Destinazione archivio gia' presente: {targetPath}.", []);
    }

    var createdPaths = new List<string>();
    var parent = archiveRoot;
    var currentPath = "/Archive";
    foreach (var segment in GetSourcePathSegments(sourceFolder.AbsolutePath))
    {
      currentPath += "/" + segment;
      if (TryFindByPath(foldersById, currentPath, out var existingFolder))
      {
        parent = existingFolder;
        continue;
      }

      var createResponse = await client.PostCreateFolderAsync(parent.Id, segment, cancellationToken);
      var content = await createResponse.Content.ReadAsStringAsync(cancellationToken);
      if (!createResponse.IsSuccessStatusCode || IsSoapFault(content))
      {
        var sanitized = CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content);
        logger.LogWarning(
          "Creazione cartella archivio fallita per {Account}. Path: {Path}. Status: {StatusCode}. Risposta: {Response}",
          settings.Email,
          currentPath,
          createResponse.StatusCode,
          sanitized);
        return new ArchiveFolderEnsureResult(false, null, $"Creazione cartella archivio fallita: {currentPath}.", createdPaths);
      }

      createdPaths.Add(currentPath);
      logger.LogInformation("Cartella archivio creata per {Account}: {Path}.", settings.Email, currentPath);
      try
      {
        foldersById = await LoadFoldersAsync(client, settings.Email, cancellationToken);
      }
      catch (InvalidOperationException ex)
      {
        logger.LogWarning(ex, "Rilettura cartelle fallita dopo creazione Archivio per {Account}.", settings.Email);
        return new ArchiveFolderEnsureResult(false, null, ex.Message, createdPaths);
      }
      catch (HttpRequestException ex)
      {
        logger.LogWarning(ex, "Errore HTTP durante rilettura cartelle Archivio per {Account}.", settings.Email);
        return new ArchiveFolderEnsureResult(false, null, $"Rilettura cartelle Archivio fallita: {ex.Message}", createdPaths);
      }
      if (!TryFindByPath(foldersById, currentPath, out parent))
      {
        return new ArchiveFolderEnsureResult(false, null, $"Cartella creata ma non riletta dal server: {currentPath}.", createdPaths);
      }
    }

    if (!TryFindByPath(foldersById, targetPath, out var targetFolder))
    {
      return new ArchiveFolderEnsureResult(false, null, $"Destinazione archivio non trovata dopo la creazione: {targetPath}.", createdPaths);
    }

    var message = createdPaths.Count == 0
      ? $"Destinazione archivio pronta: {targetPath}."
      : $"Destinazione archivio pronta: {targetPath}. Cartelle create: {createdPaths.Count}.";
    return new ArchiveFolderEnsureResult(true, targetFolder, message, createdPaths);
  }

  public static string BuildArchivePath(string sourcePath)
  {
    return "/Archive/" + string.Join('/', GetSourcePathSegments(sourcePath));
  }

  private static IReadOnlyList<string> GetSourcePathSegments(string sourcePath)
  {
    return sourcePath
      .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Where(segment => !string.Equals(segment, "Archive", StringComparison.OrdinalIgnoreCase))
      .ToArray();
  }

  private static async Task<IReadOnlyDictionary<string, MailFolder>> LoadFoldersAsync(
    CarbonioWebClient client,
    string account,
    CancellationToken cancellationToken)
  {
    var response = await client.PostGetFolderAsync(cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode || IsSoapFault(content))
    {
      throw new InvalidOperationException($"GetFolderRequest fallita durante preparazione Archivio per {account}.");
    }

    return CarbonioFolderDiagnosticService.ParseFolders(content);
  }

  private static bool TryFindByPath(IReadOnlyDictionary<string, MailFolder> foldersById, string path, out MailFolder folder)
  {
    foreach (var candidate in foldersById.Values)
    {
      if (string.Equals(candidate.AbsolutePath, path, StringComparison.OrdinalIgnoreCase))
      {
        folder = candidate;
        return true;
      }
    }

    folder = null!;
    return false;
  }

  private static bool IsSoapFault(string content)
  {
    return content.Contains("\"Fault\"", StringComparison.OrdinalIgnoreCase)
      || content.Contains("<soap:Fault", StringComparison.OrdinalIgnoreCase);
  }
}
