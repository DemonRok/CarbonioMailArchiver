using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioArchiveFolderService(ILogger<CarbonioArchiveFolderService> logger) : IArchiveFolderService
{
  public Task<FolderTreeEnsureResult> EnsureArchiveDestinationAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder sourceFolder,
    CancellationToken cancellationToken)
  {
    return EnsureDestinationAsync(settings, password, sourceFolder, "/Archive", "Archive", cancellationToken);
  }

  public Task<FolderTreeEnsureResult> EnsureTrashDestinationAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder sourceFolder,
    CancellationToken cancellationToken)
  {
    return EnsureDestinationAsync(settings, password, sourceFolder, "/Trash", "Trash", cancellationToken);
  }

  public static string BuildArchivePath(string sourcePath)
  {
    return BuildMirroredPath(sourcePath, "/Archive", "Archive");
  }

  public static string BuildTrashPath(string sourcePath)
  {
    return BuildMirroredPath(sourcePath, "/Trash", "Trash");
  }

  private async Task<FolderTreeEnsureResult> EnsureDestinationAsync(
    CarbonioConnectionSettings settings,
    string password,
    MailFolder sourceFolder,
    string rootPath,
    string rootName,
    CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      return new FolderTreeEnsureResult(false, null, validationError, []);
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      return new FolderTreeEnsureResult(false, null, loginError, []);
    }

    IReadOnlyDictionary<string, MailFolder> foldersById;
    try
    {
      foldersById = await LoadFoldersAsync(client, settings.Email, rootName, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Errore HTTP durante preparazione {RootName} per {Account}.", rootName, settings.Email);
      return new FolderTreeEnsureResult(false, null, $"Preparazione {rootName} fallita: {ex.Message}", []);
    }
    catch (InvalidOperationException ex)
    {
      logger.LogWarning(ex, "Lettura cartelle fallita durante preparazione {RootName} per {Account}.", rootName, settings.Email);
      return new FolderTreeEnsureResult(false, null, ex.Message, []);
    }

    if (!TryFindByPath(foldersById, rootPath, out var destinationRoot))
    {
      return new FolderTreeEnsureResult(false, null, $"Cartella {rootPath} non trovata. Verifica che {rootName} sia disponibile nella casella.", []);
    }

    var targetPath = BuildMirroredPath(sourceFolder.AbsolutePath, rootPath, rootName);
    if (IsPathWithinRoot(sourceFolder.AbsolutePath, rootPath))
    {
      return new FolderTreeEnsureResult(false, null, $"La sorgente selezionata e' gia' dentro {rootPath}.", []);
    }

    if (TryFindByPath(foldersById, targetPath, out var existingTarget))
    {
      return new FolderTreeEnsureResult(true, existingTarget, $"Destinazione {rootName.ToLowerInvariant()} gia' presente: {targetPath}.", []);
    }

    var createdPaths = new List<string>();
    var parent = destinationRoot;
    var currentPath = rootPath;
    foreach (var segment in GetSourcePathSegments(sourceFolder.AbsolutePath, rootName))
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
          "Creazione cartella {RootName} fallita per {Account}. Path: {Path}. Status: {StatusCode}. Risposta: {Response}",
          rootName,
          settings.Email,
          currentPath,
          createResponse.StatusCode,
          sanitized);
        return new FolderTreeEnsureResult(false, null, $"Creazione cartella {rootName.ToLowerInvariant()} fallita: {currentPath}.", createdPaths);
      }

      createdPaths.Add(currentPath);
      logger.LogInformation("Cartella {RootName} creata per {Account}: {Path}.", rootName, settings.Email, currentPath);
      try
      {
        foldersById = await LoadFoldersAsync(client, settings.Email, rootName, cancellationToken);
      }
      catch (InvalidOperationException ex)
      {
        logger.LogWarning(ex, "Rilettura cartelle fallita dopo creazione {RootName} per {Account}.", rootName, settings.Email);
        return new FolderTreeEnsureResult(false, null, ex.Message, createdPaths);
      }
      catch (HttpRequestException ex)
      {
        logger.LogWarning(ex, "Errore HTTP durante rilettura cartelle {RootName} per {Account}.", rootName, settings.Email);
        return new FolderTreeEnsureResult(false, null, $"Rilettura cartelle {rootName} fallita: {ex.Message}", createdPaths);
      }

      if (!TryFindByPath(foldersById, currentPath, out parent))
      {
        return new FolderTreeEnsureResult(false, null, $"Cartella creata ma non riletta dal server: {currentPath}.", createdPaths);
      }
    }

    if (!TryFindByPath(foldersById, targetPath, out var targetFolder))
    {
      return new FolderTreeEnsureResult(false, null, $"Destinazione {rootName.ToLowerInvariant()} non trovata dopo la creazione: {targetPath}.", createdPaths);
    }

    var message = createdPaths.Count == 0
      ? $"Destinazione {rootName.ToLowerInvariant()} pronta: {targetPath}."
      : $"Destinazione {rootName.ToLowerInvariant()} pronta: {targetPath}. Cartelle create: {createdPaths.Count}.";
    return new FolderTreeEnsureResult(true, targetFolder, message, createdPaths);
  }

  private static string BuildMirroredPath(string sourcePath, string rootPath, string rootName)
  {
    var segments = GetSourcePathSegments(sourcePath, rootName);
    return segments.Count == 0 ? rootPath : rootPath + "/" + string.Join('/', segments);
  }

  private static IReadOnlyList<string> GetSourcePathSegments(string sourcePath, string rootName)
  {
    return sourcePath
      .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Where(segment => !string.Equals(segment, rootName, StringComparison.OrdinalIgnoreCase))
      .ToArray();
  }

  private static bool IsPathWithinRoot(string path, string rootPath)
  {
    return string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase)
      || path.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase);
  }

  private static async Task<IReadOnlyDictionary<string, MailFolder>> LoadFoldersAsync(
    CarbonioWebClient client,
    string account,
    string rootName,
    CancellationToken cancellationToken)
  {
    var response = await client.PostGetFolderAsync(cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode || IsSoapFault(content))
    {
      throw new InvalidOperationException($"GetFolderRequest fallita durante preparazione {rootName} per {account}.");
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
