namespace CarbonioMailArchiver.Core.Models;

public sealed class CarbonioConnectionSettings
{
  public string BaseUrl { get; set; } = string.Empty;
  public string SoapUrl { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public bool RememberCredentials { get; set; }
  public bool AcceptUntrustedCertificates { get; set; }
  public bool DiagnosticSoapLoggingEnabled { get; set; }
  public bool AutoLoadFoldersOnStartup { get; set; }
  public int TimeoutSeconds { get; set; } = 100;
  public int PreviewMessageLimit { get; set; } = 10;
}
