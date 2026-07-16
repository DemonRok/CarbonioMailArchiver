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
      return CreateKnownFolders();
    }

    var loginError = await client.LoginAsync(password, cancellationToken);
    if (loginError is not null)
    {
      logger.LogWarning("Login Carbonio Auth fallito per GetFolderRequest {Account}: {Reason}", settings.Email, loginError);
      return CreateKnownFolders();
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
      return CreateKnownFolders();
    }

    try
    {
      var folders = ParseFolders(content);
      if (folders.Count == 0)
      {
        logger.LogInformation(
          "GetFolderRequest diagnostica senza cartelle riconosciute per {Account}. Uso fallback standard. Risposta: {Response}",
          settings.Email,
          CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content));
        return CreateKnownFolders();
      }

      logger.LogInformation("GetFolderRequest diagnostica riuscita per {Account}. Cartelle: {Count}.", settings.Email, folders.Count);
      return folders;
    }
    catch (JsonException ex)
    {
      logger.LogWarning(ex, "Parsing GetFolderRequest fallito per {Account}.", settings.Email);
      return CreateKnownFolders();
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
    if (rootFolder.ValueKind == JsonValueKind.Array)
    {
      foreach (var folder in rootFolder.EnumerateArray())
      {
        ParseFolderElement(folder, null, string.Empty, foldersById);
      }
    }
    else
    {
      ParseFolderElement(rootFolder, null, string.Empty, foldersById);
    }

    return foldersById;
  }

  private static IReadOnlyDictionary<string, MailFolder> CreateKnownFolders()
  {
    return new Dictionary<string, MailFolder>
    {
      ["1"] = new MailFolder { Id = "1", Name = "USER_ROOT", AbsolutePath = "/USER_ROOT", IsWritable = false },
      ["2"] = new MailFolder { Id = "2", Name = "Inbox", AbsolutePath = "/Inbox", ParentId = "1", IsInbox = true },
      ["3"] = new MailFolder { Id = "3", Name = "Trash", AbsolutePath = "/Trash", ParentId = "1" },
      ["4"] = new MailFolder { Id = "4", Name = "Junk", AbsolutePath = "/Junk", ParentId = "1" },
      ["5"] = new MailFolder { Id = "5", Name = "Sent", AbsolutePath = "/Sent", ParentId = "1" },
      ["6"] = new MailFolder { Id = "6", Name = "Drafts", AbsolutePath = "/Drafts", ParentId = "1" }
    };
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

    var absolutePath = ReadString(element, "absFolderPath");
    if (string.IsNullOrWhiteSpace(absolutePath))
    {
      absolutePath = string.IsNullOrEmpty(parentPath) ? $"/{name}" : $"{parentPath}/{name}";
    }

    var folder = new MailFolder
    {
      Id = id,
      Name = name,
      ParentId = parentId,
      AbsolutePath = absolutePath,
      MessageCount = ReadInt(element, "n") ?? 0,
      IsInbox = id == "2" || string.Equals(name, "Inbox", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Posta in arrivo", StringComparison.OrdinalIgnoreCase),
      IsWritable = !string.Equals(ReadString(element, "perm"), "r", StringComparison.OrdinalIgnoreCase)
    };

    foldersById[id] = folder;

    ParseChildren(element, "folder", id, absolutePath, foldersById, folder);
    ParseChildren(element, "link", id, absolutePath, foldersById, folder);

    return folder;
  }

  private static void ParseChildren(JsonElement element, string propertyName, string parentId, string parentPath, Dictionary<string, MailFolder> foldersById, MailFolder folder)
  {
    if (TryFindDirectProperty(element, propertyName, out var childFolders))
    {
      if (childFolders.ValueKind == JsonValueKind.Array)
      {
        foreach (var child in childFolders.EnumerateArray())
        {
          var childFolder = ParseFolderElement(child, parentId, parentPath, foldersById);
          if (childFolder is not null)
          {
            folder.Children.Add(childFolder);
          }
        }
      }
      else if (childFolders.ValueKind == JsonValueKind.Object)
      {
        var childFolder = ParseFolderElement(childFolders, parentId, parentPath, foldersById);
        if (childFolder is not null)
        {
          folder.Children.Add(childFolder);
        }
      }
    }
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

  private static int? ReadInt(JsonElement element, string propertyName)
  {
    if (!TryFindDirectProperty(element, propertyName, out var property))
    {
      return null;
    }

    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
    {
      return number;
    }

    return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number)
      ? number
      : null;
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
