using System.Net;
using System.Text;
using System.Text.Json;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioConnectionDiagnosticService(ILogger<CarbonioConnectionDiagnosticService> logger) : IConnectionDiagnosticService
{
  private static readonly JsonSerializerOptions JsonSerializerOptions = new()
  {
    PropertyNamingPolicy = null
  };

  public async Task<ConnectionDiagnosticResult> TestConnectionAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken)
  {
    if (settings.AcceptUntrustedCertificates)
    {
      return new ConnectionDiagnosticResult(false, "Certificati TLS non attendibili bloccati: installare la CA corretta nel trust store Windows.", null, null);
    }

    if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
    {
      return new ConnectionDiagnosticResult(false, "Base URL non valido.", null, null);
    }

    if (!Uri.TryCreate(settings.SoapUrl, UriKind.Absolute, out var soapUri))
    {
      return new ConnectionDiagnosticResult(false, "SOAP URL non valido.", null, null);
    }

    if (string.IsNullOrWhiteSpace(settings.Email))
    {
      return new ConnectionDiagnosticResult(false, "Account email mancante.", null, null);
    }

    if (string.IsNullOrEmpty(password))
    {
      return new ConnectionDiagnosticResult(false, "Password mancante.", null, null);
    }

    var cookies = new CookieContainer();
    using var handler = new HttpClientHandler
    {
      CookieContainer = cookies,
      UseCookies = true
    };

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = baseUri,
      Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 600))
    };

    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Service", "WebUI");
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Id", Guid.NewGuid().ToString());
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Model", "CarbonioMailArchiver");

    try
    {
      var loginResponse = await PostJsonAsync(
        httpClient,
        "/zx/auth/v2/login",
        new
        {
          auth_method = "password",
          user = settings.Email,
          password
        },
        cancellationToken);

      if (!loginResponse.IsSuccessStatusCode)
      {
        logger.LogWarning("Login Carbonio Auth fallito con status {StatusCode} per {Account}.", loginResponse.StatusCode, settings.Email);
        return new ConnectionDiagnosticResult(false, $"Login fallito: HTTP {(int)loginResponse.StatusCode}.", null, null);
      }

      var loginCookies = cookies.GetCookies(baseUri);
      if (loginCookies["ZX_AUTH_TOKEN"] is null && loginCookies["ZM_AUTH_TOKEN"] is null)
      {
        return new ConnectionDiagnosticResult(false, "Login completato ma nessun cookie ZX_AUTH_TOKEN/ZM_AUTH_TOKEN ricevuto.", null, null);
      }

      var noOpResponse = await PostNoOpAsync(httpClient, soapUri, settings.Email, cancellationToken);
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

      var getInfoResponse = await PostGetInfoAsync(httpClient, soapUri, settings.Email, cancellationToken);
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

  private static Task<HttpResponseMessage> PostNoOpAsync(HttpClient httpClient, Uri soapUri, string account, CancellationToken cancellationToken)
  {
    var endpoint = new Uri($"{soapUri.ToString().TrimEnd('/')}/NoOpRequest");
    var payload = new
    {
      Body = new
      {
        NoOpRequest = new
        {
          _jsns = "urn:zimbraMail"
        }
      },
      Header = new
      {
        context = new
        {
          _jsns = "urn:zimbra",
          account = new
          {
            by = "name",
            _content = account
          },
          userAgent = new
          {
            name = "CarbonioMailArchiver",
            version = "PhaseB-Diagnostic"
          }
        }
      }
    };

    return PostJsonAsync(httpClient, endpoint, payload, cancellationToken);
  }

  private static Task<HttpResponseMessage> PostGetInfoAsync(HttpClient httpClient, Uri soapUri, string account, CancellationToken cancellationToken)
  {
    var endpoint = new Uri($"{soapUri.ToString().TrimEnd('/')}/GetInfoRequest");
    var payload = new
    {
      Body = new
      {
        GetInfoRequest = new
        {
          _jsns = "urn:zimbraAccount"
        }
      },
      Header = new
      {
        context = new
        {
          _jsns = "urn:zimbra",
          account = new
          {
            by = "name",
            _content = account
          },
          userAgent = new
          {
            name = "CarbonioMailArchiver",
            version = "PhaseB-Diagnostic"
          }
        }
      }
    };

    return PostJsonAsync(httpClient, endpoint, payload, cancellationToken);
  }

  private static Task<HttpResponseMessage> PostJsonAsync(HttpClient httpClient, string requestUri, object payload, CancellationToken cancellationToken)
  {
    return PostJsonAsync(httpClient, new Uri(requestUri, UriKind.RelativeOrAbsolute), payload, cancellationToken);
  }

  private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient httpClient, Uri requestUri, object payload, CancellationToken cancellationToken)
  {
    var json = JsonSerializer.Serialize(payload, JsonSerializerOptions);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    return await httpClient.PostAsync(requestUri, content, cancellationToken);
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

  private static string SanitizeDiagnosticResponse(string response)
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
