using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioFolderDiagnosticService(ILogger<CarbonioFolderDiagnosticService> logger) : IFolderDiagnosticService
{
  public async Task<IReadOnlyDictionary<string, MailFolder>> GetFoldersByIdAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      logger.LogWarning("GetFolderRequest diagnostica non avviata per {Account}: {Reason}", settings.Email, validationError);
      return new Dictionary<string, MailFolder>();
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      logger.LogWarning("Login Carbonio Auth fallito per GetFolderRequest {Account}: {Reason}", settings.Email, loginError);
      return new Dictionary<string, MailFolder>();
    }

    var response = await client.PostGetFolderAsync(cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode || content.Contains("\"Fault\"", StringComparison.OrdinalIgnoreCase))
    {
      logger.LogWarning(
        "GetFolderRequest diagnostica fallita con status {StatusCode} per {Account}. Risposta: {Response}",
        response.StatusCode,
        settings.Email,
        CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content));
      return new Dictionary<string, MailFolder>();
    }

    try
    {
      var folders = ParseFolders(content);
      logger.LogInformation("GetFolderRequest diagnostica riuscita per {Account}. Cartelle: {Count}.", settings.Email, folders.Count);
      return folders;
    }
    catch (JsonException ex)
    {
      logger.LogWarning(ex, "Parsing GetFolderRequest fallito per {Account}.", settings.Email);
      return new Dictionary<string, MailFolder>();
    }
  }

  internal static IReadOnlyDictionary<string, MailFolder> ParseFolders(string json)
  {
    using var document = JsonDocument.Parse(json);
    if (!TryFindProperty(document.RootElement, "folder", out var rootFolder))
    {
      return new Dictionary<string, MailFolder>();
    }

    var foldersById = new Dictionary<string, MailFolder>();
    ParseFolderElement(rootFolder, null, string.Empty, foldersById);
    return foldersById;
  }

  private static MailFolder? ParseFolderElement(JsonElement element, string? parentId, string parentPath, Dictionary<string, MailFolder> foldersById)
  {
    if (element.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    var id = ReadString(element, "id");
    var name = ReadString(element, "name") ?? string.Empty;
    if (string.IsNullOrWhiteSpace(id))
    {
      return null;
    }

    var absolutePath = string.IsNullOrEmpty(parentPath) ? $"/{name}" : $"{parentPath}/{name}";
    var folder = new MailFolder
    {
      Id = id,
      Name = name,
      ParentId = parentId,
      AbsolutePath = absolutePath,
      IsInbox = id == "2" || string.Equals(name, "Inbox", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Posta in arrivo", StringComparison.OrdinalIgnoreCase),
      IsWritable = !string.Equals(ReadString(element, "perm"), "r", StringComparison.OrdinalIgnoreCase)
    };

    foldersById[id] = folder;

    if (TryFindDirectProperty(element, "folder", out var childFolders))
    {
      if (childFolders.ValueKind == JsonValueKind.Array)
      {
        foreach (var child in childFolders.EnumerateArray())
        {
          var childFolder = ParseFolderElement(child, id, absolutePath, foldersById);
          if (childFolder is not null)
          {
            folder.Children.Add(childFolder);
          }
        }
      }
      else if (childFolders.ValueKind == JsonValueKind.Object)
      {
        var childFolder = ParseFolderElement(childFolders, id, absolutePath, foldersById);
        if (childFolder is not null)
        {
          folder.Children.Add(childFolder);
        }
      }
    }

    return folder;
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
    if (!TryFindDirectProperty(element, propertyName, out var property))
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

  private static bool TryFindDirectProperty(JsonElement element, string propertyName, out JsonElement property)
  {
    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out property))
    {
      return true;
    }

    property = default;
    return false;
  }
}
