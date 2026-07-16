using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioFolderMaintenanceService(ILogger<CarbonioFolderMaintenanceService> logger) : IFolderMaintenanceService
{
  private static readonly HashSet<string> ProtectedFolderIds = new(StringComparer.OrdinalIgnoreCase)
  {
    "1",
    "2",
    "3",
    "4",
    "5",
    "6"
  };

  public async Task<FolderDeletePlanResult> AnalyzeEmptyFoldersAsync(
    CarbonioConnectionSettings settings,
    string password,
    string folderId,
    bool includeSubfolders,
    CancellationToken cancellationToken)
  {
    using var client = await CreateLoggedClientAsync(settings, password, cancellationToken);
    var folders = await LoadFoldersAsync(client, cancellationToken);
    return BuildDeletePlan(folders, folderId, includeSubfolders);
  }

  public async Task<FolderDeleteResult> DeleteEmptyFoldersAsync(
    CarbonioConnectionSettings settings,
    string password,
    string folderId,
    bool includeSubfolders,
    CancellationToken cancellationToken)
  {
    using var client = await CreateLoggedClientAsync(settings, password, cancellationToken);
    var folders = await LoadFoldersAsync(client, cancellationToken);
    var plan = BuildDeletePlan(folders, folderId, includeSubfolders);
    if (!plan.IsSuccess || plan.CandidatePaths.Count == 0)
    {
      return new FolderDeleteResult(false, plan.Message, 0);
    }

    var candidatesByPath = folders.Values
      .Where(folder => plan.CandidatePaths.Contains(folder.AbsolutePath, StringComparer.OrdinalIgnoreCase))
      .OrderByDescending(folder => folder.AbsolutePath.Count(character => character == '/'))
      .ThenByDescending(folder => folder.AbsolutePath.Length)
      .ToArray();

    var deletedCount = 0;
    foreach (var folder in candidatesByPath)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var response = await client.PostDeleteFolderAsync(folder.Id, cancellationToken);
      var content = await response.Content.ReadAsStringAsync(cancellationToken);
      if (!response.IsSuccessStatusCode || IsSoapFault(content))
      {
        var sanitized = CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content);
        logger.LogWarning(
          "Eliminazione cartella vuota fallita per {Account}. Cartella: {Folder}. Status: {StatusCode}. Risposta: {Response}",
          settings.Email,
          folder.AbsolutePath,
          response.StatusCode,
          sanitized);
        return new FolderDeleteResult(false, $"Eliminazione interrotta su {folder.AbsolutePath}. Cartelle eliminate: {deletedCount}.", deletedCount);
      }

      deletedCount++;
      logger.LogInformation("Cartella vuota eliminata per {Account}: {Folder}.", settings.Email, folder.AbsolutePath);
    }

    return new FolderDeleteResult(true, $"Cartelle vuote eliminate: {deletedCount}.", deletedCount);
  }

  private static FolderDeletePlanResult BuildDeletePlan(IReadOnlyDictionary<string, MailFolder> folders, string folderId, bool includeSubfolders)
  {
    if (!folders.TryGetValue(folderId, out var rootFolder))
    {
      return new FolderDeletePlanResult(false, "Cartella non trovata dopo il caricamento dal server.", []);
    }

    var candidates = new List<MailFolder>();
    if (includeSubfolders)
    {
      CollectEmptyDeletableFolders(rootFolder, candidates);
    }
    else if (CanDeleteSingleFolder(rootFolder))
    {
      candidates.Add(rootFolder);
    }

    if (candidates.Count == 0)
    {
      var scope = includeSubfolders ? $"sotto {rootFolder.AbsolutePath}" : rootFolder.AbsolutePath;
      return new FolderDeletePlanResult(false, $"Nessuna cartella vuota eliminabile: {scope}.", []);
    }

    var messageScope = includeSubfolders ? $"sotto {rootFolder.AbsolutePath}" : rootFolder.AbsolutePath;
    return new FolderDeletePlanResult(
      true,
      $"Cartelle vuote eliminabili {messageScope}: {candidates.Count}.",
      candidates.Select(folder => folder.AbsolutePath).ToArray());
  }

  private static bool CanDeleteSingleFolder(MailFolder folder)
  {
    return !ProtectedFolderIds.Contains(folder.Id)
      && folder.IsWritable
      && folder.MessageCount == 0
      && folder.Children.Count == 0;
  }

  private static bool CollectEmptyDeletableFolders(MailFolder folder, List<MailFolder> candidates)
  {
    var allChildrenDeletable = true;
    foreach (var child in folder.Children)
    {
      allChildrenDeletable &= CollectEmptyDeletableFolders(child, candidates);
    }

    var canDelete = !ProtectedFolderIds.Contains(folder.Id)
      && folder.IsWritable
      && folder.MessageCount == 0
      && allChildrenDeletable;
    if (canDelete)
    {
      candidates.Add(folder);
    }

    return canDelete;
  }

  private static async Task<CarbonioWebClient> CreateLoggedClientAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken)
  {
    var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      client.Dispose();
      throw new InvalidOperationException(validationError);
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      client.Dispose();
      throw new InvalidOperationException(loginError);
    }

    return client;
  }

  private static async Task<IReadOnlyDictionary<string, MailFolder>> LoadFoldersAsync(CarbonioWebClient client, CancellationToken cancellationToken)
  {
    var response = await client.PostGetFolderAsync(cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode || IsSoapFault(content))
    {
      throw new InvalidOperationException("GetFolderRequest fallita durante verifica cartella vuota.");
    }

    return CarbonioFolderDiagnosticService.ParseFolders(content);
  }

  private static bool IsSoapFault(string content)
  {
    return content.Contains("\"Fault\"", StringComparison.OrdinalIgnoreCase)
      || content.Contains("<soap:Fault", StringComparison.OrdinalIgnoreCase);
  }
}
