using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using CarbonioMailArchiver.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
  private readonly AppConfiguration _configuration;
  private readonly ICredentialStore _credentialStore;
  private readonly IOperationLogService _operationLogService;
  private readonly ILogger<MainWindowViewModel> _logger;
  private string _baseUrl = string.Empty;
  private string _soapUrl = string.Empty;
  private string _email = string.Empty;
  private string _password = string.Empty;
  private string _statusMessage = "Pronto. Configura l'endpoint Carbonio e salva la configurazione locale.";
  private bool _rememberCredentials;
  private bool _diagnosticSoapLoggingEnabled;
  private int _timeoutSeconds = 100;

  public MainWindowViewModel(
    AppConfiguration configuration,
    ICredentialStore credentialStore,
    IOperationLogService operationLogService,
    ILogger<MainWindowViewModel> logger)
  {
    _configuration = configuration;
    _credentialStore = credentialStore;
    _operationLogService = operationLogService;
    _logger = logger;

    LoadCommand = new AsyncRelayCommand(LoadAsync);
    SaveCommand = new AsyncRelayCommand(SaveAsync);
    ValidatePhaseACommand = new AsyncRelayCommand(ValidatePhaseAAsync);
    RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
    LogDirectory = operationLogService.LogDirectory;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ICommand LoadCommand { get; }
  public ICommand SaveCommand { get; }
  public ICommand ValidatePhaseACommand { get; }
  public ICommand RefreshLogsCommand { get; }
  public ObservableCollection<string> RecentLogLines { get; } = [];
  public string LogDirectory { get; }

  public string BaseUrl
  {
    get => _baseUrl;
    set => SetField(ref _baseUrl, value);
  }

  public string SoapUrl
  {
    get => _soapUrl;
    set => SetField(ref _soapUrl, value);
  }

  public string Email
  {
    get => _email;
    set => SetField(ref _email, value);
  }

  public string Password
  {
    get => _password;
    set => SetField(ref _password, value);
  }

  public bool RememberCredentials
  {
    get => _rememberCredentials;
    set => SetField(ref _rememberCredentials, value);
  }

  public bool DiagnosticSoapLoggingEnabled
  {
    get => _diagnosticSoapLoggingEnabled;
    set => SetField(ref _diagnosticSoapLoggingEnabled, value);
  }

  public int TimeoutSeconds
  {
    get => _timeoutSeconds;
    set => SetField(ref _timeoutSeconds, value);
  }

  public string StatusMessage
  {
    get => _statusMessage;
    private set => SetField(ref _statusMessage, value);
  }

  private async Task LoadAsync()
  {
    var settings = await _configuration.LoadConnectionSettingsAsync(CancellationToken.None);
    BaseUrl = settings.BaseUrl;
    SoapUrl = settings.SoapUrl;
    Email = settings.Email;
    RememberCredentials = settings.RememberCredentials;
    DiagnosticSoapLoggingEnabled = settings.DiagnosticSoapLoggingEnabled;
    TimeoutSeconds = settings.TimeoutSeconds;
    Password = settings.RememberCredentials ? await _credentialStore.ReadPasswordAsync(settings.Email, CancellationToken.None) ?? string.Empty : string.Empty;
    StatusMessage = "Configurazione caricata. La password salvata non viene mostrata nella UI.";
  }

  private async Task SaveAsync()
  {
    var settings = ToSettings();
    await _configuration.SaveConnectionSettingsAsync(settings, CancellationToken.None);

    if (settings.RememberCredentials && !string.IsNullOrWhiteSpace(settings.Email) && !string.IsNullOrEmpty(Password))
    {
      await _credentialStore.SavePasswordAsync(settings.Email, Password, CancellationToken.None);
    }

    if (!settings.RememberCredentials && !string.IsNullOrWhiteSpace(settings.Email))
    {
      await _credentialStore.DeletePasswordAsync(settings.Email, CancellationToken.None);
    }

    _logger.LogInformation("Configurazione locale salvata per {Account}.", settings.Email);
    StatusMessage = "Configurazione salvata. Le password non sono scritte nel JSON.";
    await RefreshLogsAsync();
  }

  private async Task ValidatePhaseAAsync()
  {
    var settings = ToSettings();
    var issues = new List<string>();

    if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
    {
      issues.Add("Base URL non valido.");
    }

    if (!Uri.TryCreate(settings.SoapUrl, UriKind.Absolute, out _))
    {
      issues.Add("SOAP URL non valido.");
    }

    if (string.IsNullOrWhiteSpace(settings.Email))
    {
      issues.Add("Account email mancante.");
    }

    if (settings.AcceptUntrustedCertificates)
    {
      issues.Add("Certificati TLS non attendibili non accettati in Fase A.");
    }

    if (issues.Count > 0)
    {
      StatusMessage = string.Join(" ", issues);
      return;
    }

    _logger.LogInformation("Validazione Fase A completata per {Account}. Auth SOAP reale non ancora implementata.", settings.Email);
    StatusMessage = "Validazione Fase A completata. Auth SOAP reale prevista in Fase B.";
    await RefreshLogsAsync();
  }

  private async Task RefreshLogsAsync()
  {
    RecentLogLines.Clear();
    var lines = await _operationLogService.ReadRecentLinesAsync(200, CancellationToken.None);
    foreach (var line in lines)
    {
      RecentLogLines.Add(line);
    }
  }

  private CarbonioConnectionSettings ToSettings()
  {
    return new CarbonioConnectionSettings
    {
      BaseUrl = BaseUrl.Trim(),
      SoapUrl = SoapUrl.Trim(),
      Email = Email.Trim(),
      RememberCredentials = RememberCredentials,
      AcceptUntrustedCertificates = false,
      DiagnosticSoapLoggingEnabled = DiagnosticSoapLoggingEnabled,
      TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600)
    };
  }

  private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
    {
      return;
    }

    field = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
