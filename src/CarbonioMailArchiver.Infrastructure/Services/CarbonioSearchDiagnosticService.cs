using System.Globalization;
using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using CarbonioMailArchiver.Core.Services;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioSearchDiagnosticService(
  IFolderDiagnosticService folderDiagnosticService,
  ILogger<CarbonioSearchDiagnosticService> logger) : ISearchDiagnosticService
{
  private readonly MailQueryBuilder _queryBuilder = new();

  public async Task<SearchDiagnosticResult> SearchInboxBeforeAsync(CarbonioConnectionSettings settings, string password, MailSearchRequest request, CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
        return new SearchDiagnosticResult(false, validationError, [], null, false, new Dictionary<string, MailFolder>());
    }

    try
    {
      var loginError = await client.LoginAsync(password, cancellationToken);
      if (loginError is not null)
      {
        logger.LogWarning("Login Carbonio Auth fallito per ricerca diagnostica {Account}: {Reason}", settings.Email, loginError);
        return new SearchDiagnosticResult(false, loginError, [], null, false, new Dictionary<string, MailFolder>());
      }

      var query = _queryBuilder.BuildInboxBeforeQuery(request);
      var response = await client.PostSearchAsync(query, Math.Clamp(request.Limit, 1, 50), Math.Max(request.Offset, 0), cancellationToken);
      var content = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        logger.LogWarning(
          "SearchRequest diagnostica fallita con status {StatusCode} per {Account}. Risposta: {Response}",
          response.StatusCode,
          settings.Email,
          CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content));
        return new SearchDiagnosticResult(false, $"SearchRequest fallita: HTTP {(int)response.StatusCode}.", [], null, false, new Dictionary<string, MailFolder>());
      }

      if (content.Contains("\"Fault\"", StringComparison.OrdinalIgnoreCase))
      {
        logger.LogWarning(
          "SearchRequest diagnostica ha restituito un fault per {Account}. Risposta: {Response}",
          settings.Email,
          CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content));
        return new SearchDiagnosticResult(false, "SearchRequest ha restituito un fault SOAP.", [], null, false, new Dictionary<string, MailFolder>());
      }

      var foldersById = await folderDiagnosticService.GetFoldersByIdAsync(settings, password, cancellationToken);
      var result = ParseSearchResult(content, foldersById);
      logger.LogInformation(
        "SearchRequest diagnostica riuscita per {Account}. Query: {Query}. Offset: {Offset}. Messaggi: {Count}.",
        settings.Email,
        query,
        request.Offset,
        result.Messages.Count);
      return result;
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Errore HTTP durante SearchRequest diagnostica per {Account}.", settings.Email);
      return new SearchDiagnosticResult(false, $"Errore HTTP: {ex.Message}", [], null, false, new Dictionary<string, MailFolder>());
    }
    catch (JsonException ex)
    {
      logger.LogWarning(ex, "Parsing risposta SearchRequest fallito per {Account}.", settings.Email);
      return new SearchDiagnosticResult(false, $"Risposta SearchRequest non riconosciuta: {ex.Message}", [], null, false, new Dictionary<string, MailFolder>());
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(ex, "Timeout durante SearchRequest diagnostica per {Account}.", settings.Email);
      return new SearchDiagnosticResult(false, "Timeout durante la ricerca diagnostica.", [], null, false, new Dictionary<string, MailFolder>());
    }
  }

  private static SearchDiagnosticResult ParseSearchResult(string json, IReadOnlyDictionary<string, MailFolder> foldersById)
  {
    using var document = JsonDocument.Parse(json);
    if (!TryFindProperty(document.RootElement, "SearchResponse", out var searchResponse))
    {
      return new SearchDiagnosticResult(false, "Risposta SearchRequest senza SearchResponse.", [], null, false, foldersById);
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

    var totalCount = ReadInt(searchResponse, "more") == 1 ? ReadInt(searchResponse, "total") : ReadInt(searchResponse, "num");
    var hasMore = ReadBool(searchResponse, "more");
    return new SearchDiagnosticResult(true, $"SearchRequest completata. Messaggi letti: {messages.Count}.", messages, totalCount, hasMore, foldersById);
  }

  private static MailMessageSummary ParseMessage(JsonElement item)
  {
    var id = ReadString(item, "id") ?? string.Empty;
    var from = ReadEmailParticipant(item);
    var subject = ReadString(item, "su") ?? string.Empty;
    var folderId = ReadString(item, "l") ?? string.Empty;
    var size = ReadLong(item, "s");
    var date = ReadUnixMilliseconds(item, "d");
    return new MailMessageSummary(id, date, from, subject, size, folderId);
  }

  private static string ReadEmailParticipant(JsonElement message)
  {
    if (!TryFindProperty(message, "e", out var participants))
    {
      return string.Empty;
    }

    if (participants.ValueKind == JsonValueKind.Array)
    {
      foreach (var participant in participants.EnumerateArray())
      {
        if (string.Equals(ReadString(participant, "t"), "f", StringComparison.OrdinalIgnoreCase))
        {
          return ReadString(participant, "a") ?? ReadString(participant, "p") ?? string.Empty;
        }
      }
    }

    if (participants.ValueKind == JsonValueKind.Object)
    {
      return ReadString(participants, "a") ?? ReadString(participants, "p") ?? string.Empty;
    }

    return string.Empty;
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

    if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
    {
      return number;
    }

    return null;
  }

  private static long? ReadLong(JsonElement element, string propertyName)
  {
    if (!TryFindDirectProperty(element, propertyName, out var property))
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
    if (!TryFindDirectProperty(element, propertyName, out var property))
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
