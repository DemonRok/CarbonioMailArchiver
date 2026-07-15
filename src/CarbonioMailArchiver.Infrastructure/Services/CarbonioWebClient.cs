using System.Net;
using System.Text;
using System.Text.Json;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Infrastructure.Services;

internal sealed class CarbonioWebClient : IDisposable
{
  private static readonly JsonSerializerOptions JsonSerializerOptions = new()
  {
    PropertyNamingPolicy = null
  };

  private readonly Uri _soapUri;
  private readonly string _account;
  private readonly CookieContainer _cookies;
  private readonly HttpClientHandler _handler;
  private readonly HttpClient _httpClient;

  private CarbonioWebClient(CarbonioConnectionSettings settings, Uri baseUri, Uri soapUri)
  {
    _soapUri = soapUri;
    _account = settings.Email;
    _cookies = new CookieContainer();
    _handler = new HttpClientHandler
    {
      CookieContainer = _cookies,
      UseCookies = true
    };

    _httpClient = new HttpClient(_handler)
    {
      BaseAddress = baseUri,
      Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 600))
    };

    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Service", "WebUI");
    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Id", Guid.NewGuid().ToString());
    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Model", "CarbonioMailArchiver");
  }

  public static CarbonioWebClient Create(CarbonioConnectionSettings settings, out string? validationError)
  {
    validationError = ValidateSettings(settings, out var baseUri, out var soapUri);
    return new CarbonioWebClient(settings, baseUri ?? new Uri("https://localhost"), soapUri ?? new Uri("https://localhost/service/soap"));
  }

  public async Task<string?> LoginAsync(string password, CancellationToken cancellationToken)
  {
    if (string.IsNullOrEmpty(password))
    {
      return "Password mancante.";
    }

    var loginResponse = await PostJsonAsync(
      "/zx/auth/v2/login",
      new
      {
        auth_method = "password",
        user = _account,
        password
      },
      cancellationToken);

    if (!loginResponse.IsSuccessStatusCode)
    {
      return $"Login fallito: HTTP {(int)loginResponse.StatusCode}.";
    }

    var loginCookies = _cookies.GetCookies(_httpClient.BaseAddress!);
    if (loginCookies["ZX_AUTH_TOKEN"] is null && loginCookies["ZM_AUTH_TOKEN"] is null)
    {
      return "Login completato ma nessun cookie ZX_AUTH_TOKEN/ZM_AUTH_TOKEN ricevuto.";
    }

    return null;
  }

  public Task<HttpResponseMessage> PostNoOpAsync(CancellationToken cancellationToken)
  {
    var payload = BuildSoapPayload("NoOpRequest", new { _jsns = "urn:zimbraMail" });
    return PostSoapJsonAsync("NoOpRequest", payload, cancellationToken);
  }

  public Task<HttpResponseMessage> PostGetInfoAsync(CancellationToken cancellationToken)
  {
    var payload = BuildSoapPayload("GetInfoRequest", new { _jsns = "urn:zimbraAccount" });
    return PostSoapJsonAsync("GetInfoRequest", payload, cancellationToken);
  }

  public Task<HttpResponseMessage> PostGetFolderAsync(CancellationToken cancellationToken)
  {
    var payload = BuildSoapPayload(
      "GetFolderRequest",
      new
      {
        _jsns = "urn:zimbraMail",
        visible = 1
      });
    return PostSoapJsonAsync("GetFolderRequest", payload, cancellationToken);
  }

  public Task<HttpResponseMessage> PostSearchAsync(string query, int limit, int offset, CancellationToken cancellationToken)
  {
    var request = new
    {
      _jsns = "urn:zimbraMail",
      query,
      types = "message",
      limit,
      offset,
      sortBy = "dateDesc",
      fetch = 1
    };
    var payload = BuildSoapPayload("SearchRequest", request);
    return PostSoapJsonAsync("SearchRequest", payload, cancellationToken);
  }

  public Task<HttpResponseMessage> PostMoveMessagesAsync(IReadOnlyList<string> messageIds, string destinationFolderId, CancellationToken cancellationToken)
  {
    var request = new
    {
      _jsns = "urn:zimbraMail",
      action = new
      {
        id = string.Join(",", messageIds),
        op = "move",
        l = destinationFolderId
      }
    };
    var payload = BuildSoapPayload("MsgActionRequest", request);
    return PostSoapJsonAsync("MsgActionRequest", payload, cancellationToken);
  }

  public void Dispose()
  {
    _httpClient.Dispose();
    _handler.Dispose();
  }

  private static string? ValidateSettings(CarbonioConnectionSettings settings, out Uri? baseUri, out Uri? soapUri)
  {
    baseUri = null;
    soapUri = null;

    if (settings.AcceptUntrustedCertificates)
    {
      return "Certificati TLS non attendibili bloccati: installare la CA corretta nel trust store Windows.";
    }

    if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out baseUri))
    {
      return "Base URL non valido.";
    }

    if (!Uri.TryCreate(settings.SoapUrl, UriKind.Absolute, out soapUri))
    {
      return "SOAP URL non valido.";
    }

    if (string.IsNullOrWhiteSpace(settings.Email))
    {
      return "Account email mancante.";
    }

    return null;
  }

  private object BuildSoapPayload(string requestName, object request)
  {
    var body = new Dictionary<string, object>
    {
      [requestName] = request
    };

    return new
    {
      Body = body,
      Header = new
      {
        context = new
        {
          _jsns = "urn:zimbra",
          account = new
          {
            by = "name",
            _content = _account
          },
          userAgent = new
          {
            name = "CarbonioMailArchiver",
            version = "PhaseB-Diagnostic"
          }
        }
      }
    };
  }

  private Task<HttpResponseMessage> PostSoapJsonAsync(string requestName, object payload, CancellationToken cancellationToken)
  {
    var endpoint = new Uri($"{_soapUri.ToString().TrimEnd('/')}/{requestName}");
    return PostJsonAsync(endpoint, payload, cancellationToken);
  }

  private Task<HttpResponseMessage> PostJsonAsync(string requestUri, object payload, CancellationToken cancellationToken)
  {
    return PostJsonAsync(new Uri(requestUri, UriKind.RelativeOrAbsolute), payload, cancellationToken);
  }

  private async Task<HttpResponseMessage> PostJsonAsync(Uri requestUri, object payload, CancellationToken cancellationToken)
  {
    var json = JsonSerializer.Serialize(payload, JsonSerializerOptions);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    return await _httpClient.PostAsync(requestUri, content, cancellationToken);
  }
}
