using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
  private readonly IConnectionDiagnosticService _connectionDiagnosticService;
  private readonly ISearchDiagnosticService _searchDiagnosticService;
  private readonly ILogger<MainWindowViewModel> _logger;
  private string _baseUrl = string.Empty;
  private string _soapUrl = string.Empty;
  private string _email = string.Empty;
  private string _password = string.Empty;
  private string _recentLogText = string.Empty;
  private string _searchBeforeDate = DateTime.Today.ToString("yyyy-MM-dd");
  private string _statusMessage = "Pronto. Configura l'endpoint Carbonio e salva la configurazione locale.";
  private bool _rememberCredentials;
  private bool _diagnosticSoapLoggingEnabled;
  private int _timeoutSeconds = 100;

  public MainWindowViewModel(
    AppConfiguration configuration,
    ICredentialStore credentialStore,
    IOperationLogService operationLogService,
    IConnectionDiagnosticService connectionDiagnosticService,
    ISearchDiagnosticService searchDiagnosticService,
    ILogger<MainWindowViewModel> logger)
  {
    _configuration = configuration;
    _credentialStore = credentialStore;
    _operationLogService = operationLogService;
    _connectionDiagnosticService = connectionDiagnosticService;
    _searchDiagnosticService = searchDiagnosticService;
    _logger = logger;

    LoadCommand = new AsyncRelayCommand(LoadAsync);
    SaveCommand = new AsyncRelayCommand(SaveAsync);
    TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    TestSearchCommand = new AsyncRelayCommand(TestSearchAsync);
    RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
    CopyLogsCommand = new AsyncRelayCommand(CopyLogsAsync);
    LogDirectory = operationLogService.LogDirectory;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ICommand LoadCommand { get; }
  public ICommand SaveCommand { get; }
  public ICommand TestConnectionCommand { get; }
  public ICommand TestSearchCommand { get; }
  public ICommand RefreshLogsCommand { get; }
  public ICommand CopyLogsCommand { get; }
  public ObservableCollection<string> RecentLogLines { get; } = [];
  public ObservableCollection<MailMessagePreviewViewModel> PreviewMessages { get; } = [];
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

  public string RecentLogText
  {
    get => _recentLogText;
    private set => SetField(ref _recentLogText, value);
  }

  public string SearchBeforeDate
  {
    get => _searchBeforeDate;
    set => SetField(ref _searchBeforeDate, value);
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

  private async Task TestConnectionAsync()
  {
    var settings = ToSettings();
    var validationError = ValidateConnectionFields(settings);
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    var password = await GetPasswordAsync(settings);

    StatusMessage = "Test connessione in corso...";
    var result = await _connectionDiagnosticService.TestConnectionAsync(settings, password, CancellationToken.None);
    StatusMessage = result.IsSuccess
      ? $"{result.Message} Account: {result.AccountName}. Versione: {result.ServerVersion ?? "non rilevata"}."
      : result.Message;
    await RefreshLogsAsync();
  }

  private async Task TestSearchAsync()
  {
    var settings = ToSettings();
    var validationError = ValidateConnectionFields(settings);
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    if (!DateOnly.TryParse(SearchBeforeDate, out var beforeDate))
    {
      StatusMessage = "Data ricerca non valida. Usa formato yyyy-MM-dd.";
      return;
    }

    var password = await GetPasswordAsync(settings);
    var request = new MailSearchRequest(beforeDate, 10);
    PreviewMessages.Clear();
    StatusMessage = "Ricerca diagnostica in corso...";
    var result = await _searchDiagnosticService.SearchInboxBeforeAsync(settings, password, request, CancellationToken.None);
    if (!result.IsSuccess)
    {
      StatusMessage = result.Message;
      await RefreshLogsAsync();
      return;
    }

    foreach (var message in result.Messages)
    {
      PreviewMessages.Add(new MailMessagePreviewViewModel(message, result.FoldersById));
    }

    StatusMessage = $"{result.Message} Totale dichiarato: {result.TotalCount?.ToString() ?? "non rilevato"}. Altri risultati: {(result.HasMore ? "si" : "no")}.";
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

    RecentLogText = string.Join(Environment.NewLine, lines);
  }

  private Task CopyLogsAsync()
  {
    if (!string.IsNullOrWhiteSpace(RecentLogText))
    {
      Clipboard.SetText(RecentLogText);
      StatusMessage = "Log copiato negli appunti.";
    }

    return Task.CompletedTask;
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

  private async Task<string> GetPasswordAsync(CarbonioConnectionSettings settings)
  {
    if (!string.IsNullOrEmpty(Password))
    {
      return Password;
    }

    return settings.RememberCredentials
      ? await _credentialStore.ReadPasswordAsync(settings.Email, CancellationToken.None) ?? string.Empty
      : string.Empty;
  }

  private static string? ValidateConnectionFields(CarbonioConnectionSettings settings)
  {
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
      issues.Add("Certificati TLS non attendibili non accettati.");
    }

    return issues.Count == 0 ? null : string.Join(" ", issues);
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
