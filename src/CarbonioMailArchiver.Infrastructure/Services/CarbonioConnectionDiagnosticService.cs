using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioConnectionDiagnosticService(ILogger<CarbonioConnectionDiagnosticService> logger) : IConnectionDiagnosticService
{
  public async Task<ConnectionDiagnosticResult> TestConnectionAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken)
  {
    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      return new ConnectionDiagnosticResult(false, validationError, null, null);
    }

    try
    {
      var loginError = await client.LoginAsync(password, cancellationToken);
      if (loginError is not null)
      {
        logger.LogWarning("Login Carbonio Auth fallito per {Account}: {Reason}", settings.Email, loginError);
        return new ConnectionDiagnosticResult(false, loginError, null, null);
      }

      var noOpResponse = await client.PostNoOpAsync(cancellationToken);
      if (!noOpResponse.IsSuccessStatusCode)
      {
        var noOpContent = await noOpResponse.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
          "NoOpRequest fallita con status {StatusCode} per {Account}. Risposta: {Response}",
          noOpResponse.StatusCode,
          settings.Email,
          SanitizeDiagnosticResponse(noOpContent));
        return new ConnectionDiagnosticResult(false, $"Login riuscito, ma NoOpRequest fallita: HTTP {(int)noOpResponse.StatusCode}.", settings.Email, null);
      }

      var getInfoResponse = await client.PostGetInfoAsync(cancellationToken);
      if (!getInfoResponse.IsSuccessStatusCode)
      {
        var getInfoErrorContent = await getInfoResponse.Content.ReadAsStringAsync(cancellationToken);
        logger.LogInformation(
          "GetInfoRequest opzionale fallita con status {StatusCode} per {Account}. Risposta: {Response}",
          getInfoResponse.StatusCode,
          settings.Email,
          SanitizeDiagnosticResponse(getInfoErrorContent));
        logger.LogInformation("Test connessione Carbonio riuscito tramite NoOpRequest per {Account}.", settings.Email);
        return new ConnectionDiagnosticResult(true, $"Login Carbonio Auth e NoOpRequest completati. GetInfoRequest opzionale fallita con HTTP {(int)getInfoResponse.StatusCode}.", settings.Email, null);
      }

      var content = await getInfoResponse.Content.ReadAsStringAsync(cancellationToken);
      if (content.Contains("Fault", StringComparison.OrdinalIgnoreCase))
      {
        logger.LogInformation("GetInfoRequest opzionale ha restituito un fault SOAP per {Account}.", settings.Email);
        logger.LogInformation("Test connessione Carbonio riuscito tramite NoOpRequest per {Account}.", settings.Email);
        return new ConnectionDiagnosticResult(true, "Login Carbonio Auth e NoOpRequest completati. GetInfoRequest opzionale ha restituito un fault SOAP.", settings.Email, null);
      }

      var serverVersion = TryReadStringProperty(content, "version");
      logger.LogInformation("Test connessione Carbonio riuscito per {Account}.", settings.Email);
      return new ConnectionDiagnosticResult(true, "Login Carbonio Auth e GetInfoRequest completati.", settings.Email, serverVersion);
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Errore HTTP durante test connessione Carbonio per {Account}.", settings.Email);
      return new ConnectionDiagnosticResult(false, $"Errore HTTP: {ex.Message}", null, null);
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(ex, "Timeout durante test connessione Carbonio per {Account}.", settings.Email);
      return new ConnectionDiagnosticResult(false, "Timeout durante il test connessione.", null, null);
    }
  }

  internal static string SanitizeDiagnosticResponse(string response)
  {
    if (string.IsNullOrWhiteSpace(response))
    {
      return "<vuota>";
    }

    var compact = response
      .Replace("\r", string.Empty, StringComparison.Ordinal)
      .Replace("\n", " ", StringComparison.Ordinal);

    if (compact.Length > 500)
    {
      compact = compact[..500] + "...";
    }

    return compact;
  }

  private static string? TryReadStringProperty(string json, string propertyName)
  {
    try
    {
      using var document = JsonDocument.Parse(json);
      return FindStringProperty(document.RootElement, propertyName);
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private static string? FindStringProperty(JsonElement element, string propertyName)
  {
    if (element.ValueKind == JsonValueKind.Object)
    {
      foreach (var property in element.EnumerateObject())
      {
        if (property.NameEquals(propertyName) && property.Value.ValueKind == JsonValueKind.String)
        {
          return property.Value.GetString();
        }

        var nested = FindStringProperty(property.Value, propertyName);
        if (nested is not null)
        {
          return nested;
        }
      }
    }

    if (element.ValueKind == JsonValueKind.Array)
    {
      foreach (var item in element.EnumerateArray())
      {
        var nested = FindStringProperty(item, propertyName);
        if (nested is not null)
        {
          return nested;
        }
      }
    }

    return null;
  }
}
