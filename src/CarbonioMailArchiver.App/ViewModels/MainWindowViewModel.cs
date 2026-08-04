using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using CarbonioMailArchiver.Infrastructure.Configuration;
using CarbonioMailArchiver.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CarbonioMailArchiver.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
  private readonly AppConfiguration _configuration;
  private readonly ICredentialStore _credentialStore;
  private readonly IOperationLogService _operationLogService;
  private readonly IConnectionDiagnosticService _connectionDiagnosticService;
  private readonly ISearchDiagnosticService _searchDiagnosticService;
  private readonly IFolderDiagnosticService _folderDiagnosticService;
  private readonly IArchiveFolderService _archiveFolderService;
  private readonly IFolderMaintenanceService _folderMaintenanceService;
  private readonly IMoveDiagnosticService _moveDiagnosticService;
  private readonly IMessageDownloadService _messageDownloadService;
  private readonly IOperationReportService _operationReportService;
  private readonly IArchiveExportService _archiveExportService;
  private readonly ILogger<MainWindowViewModel> _logger;
  private string _baseUrl = string.Empty;
  private string _soapUrl = string.Empty;
  private string _email = string.Empty;
  private string _password = string.Empty;
  private string _recentLogText = string.Empty;
  private string? _lastReportPath;
  private string _lastSourceFolderId = string.Empty;
  private string _lastDestinationFolderId = string.Empty;
  private string _searchBeforeDate = DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
  private string _statusMessage = "Pronto. Configura l'endpoint Carbonio e salva la configurazione locale.";
  private FolderSelectionViewModel? _selectedSourceFolder;
  private FolderSelectionViewModel? _selectedDestinationFolder;
  private bool _rememberCredentials;
  private bool _diagnosticSoapLoggingEnabled;
  private bool _autoLoadFoldersOnStartup;
  private bool _useArchiveDestination;
  private bool _includeSourceSubfolders;
  private bool _promptReportExportAfterMove = true;
  private string _downloadRootDirectory = string.Empty;
  private int _downloadSpeedLimitKbps;
  private int _downloadRetryCount = 3;
  private int _downloadRetryDelaySeconds = 10;
  private SevenZipCompressionLevelOption _selectedSevenZipCompressionLevel = SevenZipCompressionLevelOption.Default;
  private bool _downloadVerificationSucceeded;
  private bool _isMoveInProgress;
  private int _timeoutSeconds = 100;
  private int _previewMessageLimit = 10;
  private int _batchSize = 250;
  private int _maxMessagesToMove;
  private int _moveProgressPercentage;
  private bool _isMoveProgressIndeterminate;
  private string _moveProgressText = "Nessuno spostamento in corso.";
  private string _moveProgressPercentText = string.Empty;
  private string _moveBatchText = string.Empty;
  private string _moveDetailText = "Nessuno spostamento in corso.";
  private string _operationMetricsText = string.Empty;
  private string _operationDownloadedText = string.Empty;
  private string _operationElapsedText = string.Empty;
  private string _operationSpeedText = string.Empty;
  private string _operationEtaText = string.Empty;
  private string _operationFolderText = string.Empty;
  private string _operationFileText = string.Empty;
  private DateTimeOffset _operationStartedAt = DateTimeOffset.MinValue;
  private TimeSpan _operationSpeedWarmupThreshold = TimeSpan.FromSeconds(50);
  private TimeSpan _operationEtaWarmupThreshold = TimeSpan.FromSeconds(75);
  private string _stableOperationEtaText = "ETA in calcolo";
  private DateTimeOffset _lastOperationEtaUpdate = DateTimeOffset.MinValue;
  private bool _operationMetricsVisible;
  private int _operationCompletedCount;
  private int? _operationTotalCount;
  private string _operationCurrentLabel = string.Empty;
  private MailDownloadProgress? _lastDownloadProgress;
  private string _stableDownloadEtaText = "ETA in calcolo";
  private DateTimeOffset _lastDownloadEtaUpdate = DateTimeOffset.MinValue;
  private DateTimeOffset _downloadStartedAt = DateTimeOffset.MinValue;
  private TimeSpan _downloadSpeedWarmupThreshold = TimeSpan.FromSeconds(50);
  private TimeSpan _downloadEtaWarmupThreshold = TimeSpan.FromSeconds(75);
  private DispatcherTimer? _downloadMetricsTimer;
  private DispatcherTimer? _operationMetricsTimer;
  private CancellationTokenSource? _moveCancellationTokenSource;
  private readonly AsyncRelayCommand _moveAllSearchResultsCommand;
  private readonly AsyncRelayCommand _cancelMoveCommand;
  private readonly ListCollectionView _recentLogEntriesView;
  private const string RepositoryUrl = "https://github.com/DemonRok/CarbonioMailArchiver";
  private const string ReleasesUrl = "https://github.com/DemonRok/CarbonioMailArchiver/releases";
  private const string IssuesUrl = "https://github.com/DemonRok/CarbonioMailArchiver/issues";
  private static readonly TimeSpan DownloadEtaRefreshInterval = TimeSpan.FromSeconds(5);
  private static readonly string CurrentVersion =
    typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
    ?? "dev";
  private sealed record SourceFolderScan(
    FolderSelectionViewModel SourceFolder,
    FolderSelectionViewModel PlannedDestinationFolder,
    bool IsSuccess,
    string Message,
    IReadOnlyList<string> MessageIds);

  public sealed record SevenZipCompressionLevelOption(string Name, int Level)
  {
    public static SevenZipCompressionLevelOption Default { get; } = new("Bilanciata", 5);
    public override string ToString() => Name;
  }

  public MainWindowViewModel(
    AppConfiguration configuration,
    ICredentialStore credentialStore,
    IOperationLogService operationLogService,
    IConnectionDiagnosticService connectionDiagnosticService,
    ISearchDiagnosticService searchDiagnosticService,
    IFolderDiagnosticService folderDiagnosticService,
    IArchiveFolderService archiveFolderService,
    IFolderMaintenanceService folderMaintenanceService,
    IMoveDiagnosticService moveDiagnosticService,
    IMessageDownloadService messageDownloadService,
    IOperationReportService operationReportService,
    IArchiveExportService archiveExportService,
    ILogger<MainWindowViewModel> logger)
  {
    _configuration = configuration;
    _credentialStore = credentialStore;
    _operationLogService = operationLogService;
    _connectionDiagnosticService = connectionDiagnosticService;
    _searchDiagnosticService = searchDiagnosticService;
    _folderDiagnosticService = folderDiagnosticService;
    _archiveFolderService = archiveFolderService;
    _folderMaintenanceService = folderMaintenanceService;
    _moveDiagnosticService = moveDiagnosticService;
    _messageDownloadService = messageDownloadService;
    _operationReportService = operationReportService;
    _archiveExportService = archiveExportService;
    _logger = logger;

    LoadCommand = new AsyncRelayCommand(LoadAsync);
    SaveCommand = new AsyncRelayCommand(SaveAsync);
    TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    LoadFoldersCommand = new AsyncRelayCommand(LoadFoldersAsync);
    TestSearchCommand = new AsyncRelayCommand(TestSearchAsync);
    SimulateMoveCommand = new AsyncRelayCommand(SimulateMoveAsync);
    MovePreviewCommand = new AsyncRelayCommand(MovePreviewAsync);
    _moveAllSearchResultsCommand = new AsyncRelayCommand(MoveAllSearchResultsAsync, () => !IsMoveInProgress);
    _cancelMoveCommand = new AsyncRelayCommand(CancelMoveAsync, () => IsMoveInProgress);
    MoveAllSearchResultsCommand = _moveAllSearchResultsCommand;
    CancelMoveCommand = _cancelMoveCommand;
    DownloadMessagesCommand = new AsyncRelayCommand(DownloadMessagesAsync, () => !IsMoveInProgress);
    VerifyDownloadedMessagesCommand = new AsyncRelayCommand(VerifyDownloadedMessagesAsync, () => !IsMoveInProgress);
    CompressMessagesCommand = new AsyncRelayCommand(CompressMessagesAsync, () => !IsMoveInProgress && _downloadVerificationSucceeded);
    RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
    CopyLogsCommand = new AsyncRelayCommand(CopyLogsAsync);
    ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);
    LogDirectory = operationLogService.LogDirectory;
    OpenAppDataCommand = new AsyncRelayCommand(() => OpenPathAsync(ApplicationDirectory));
    OpenLogsCommand = new AsyncRelayCommand(() => OpenPathAsync(LogDirectory));
    OpenReportsCommand = new AsyncRelayCommand(() => OpenPathAsync(ReportDirectory));
    OpenDownloadsCommand = new AsyncRelayCommand(() => OpenPathAsync(GetEffectiveDownloadRootDirectory()));
    OpenLastReportCommand = new AsyncRelayCommand(OpenLastReportAsync);
    OpenRepositoryCommand = new AsyncRelayCommand(() => OpenPathAsync(RepositoryUrl));
    OpenReleasesCommand = new AsyncRelayCommand(() => OpenPathAsync(ReleasesUrl));
    OpenLicenseCommand = new AsyncRelayCommand(OpenLicenseAsync);
    ReportIssueCommand = new AsyncRelayCommand(() => OpenPathAsync(IssuesUrl));
    RestoreConfigurationDefaultsCommand = new AsyncRelayCommand(RestoreConfigurationDefaultsAsync);
    BrowseDownloadRootDirectoryCommand = new AsyncRelayCommand(BrowseDownloadRootDirectoryAsync);
    DeleteSourceFolderIfEmptyCommand = new AsyncRelayCommand(() => DeleteSelectedFolderIfEmptyAsync(SelectedSourceFolder, "sorgente"));
    DeleteDestinationFolderIfEmptyCommand = new AsyncRelayCommand(() => DeleteSelectedFolderIfEmptyAsync(SelectedDestinationFolder, "destinazione"));

    _recentLogEntriesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RecentLogEntries);
    _recentLogEntriesView.Filter = FilterLogEntry;
    _recentLogEntriesView.SortDescriptions.Add(new SortDescription(nameof(LogEntryViewModel.TimestampSortKey), ListSortDirection.Descending));
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ICommand LoadCommand { get; }
  public ICommand SaveCommand { get; }
  public ICommand TestConnectionCommand { get; }
  public ICommand LoadFoldersCommand { get; }
  public ICommand TestSearchCommand { get; }
  public ICommand SimulateMoveCommand { get; }
  public ICommand MovePreviewCommand { get; }
  public ICommand MoveAllSearchResultsCommand { get; }
  public ICommand CancelMoveCommand { get; }
  public ICommand DownloadMessagesCommand { get; }
  public ICommand VerifyDownloadedMessagesCommand { get; }
  public ICommand CompressMessagesCommand { get; }
  public ICommand RefreshLogsCommand { get; }
  public ICommand CopyLogsCommand { get; }
  public ICommand ClearLogsCommand { get; }
  public ICommand OpenAppDataCommand { get; }
  public ICommand OpenLogsCommand { get; }
  public ICommand OpenReportsCommand { get; }
  public ICommand OpenDownloadsCommand { get; }
  public ICommand OpenLastReportCommand { get; }
  public ICommand OpenRepositoryCommand { get; }
  public ICommand OpenReleasesCommand { get; }
  public ICommand OpenLicenseCommand { get; }
  public ICommand ReportIssueCommand { get; }
  public ICommand RestoreConfigurationDefaultsCommand { get; }
  public ICommand BrowseDownloadRootDirectoryCommand { get; }
  public ICommand DeleteSourceFolderIfEmptyCommand { get; }
  public ICommand DeleteDestinationFolderIfEmptyCommand { get; }
  public ICommand DecreasePreviewMessageLimitCommand => new AsyncRelayCommand(() => UpdatePreviewMessageLimitAsync(-1));
  public ICommand IncreasePreviewMessageLimitCommand => new AsyncRelayCommand(() => UpdatePreviewMessageLimitAsync(1));
  public ICommand DecreaseBatchSizeCommand => new AsyncRelayCommand(() => UpdateBatchSizeAsync(-1));
  public ICommand IncreaseBatchSizeCommand => new AsyncRelayCommand(() => UpdateBatchSizeAsync(1));
  public ICommand DecreaseMaxMessagesToMoveCommand => new AsyncRelayCommand(() => UpdateMaxMessagesToMoveAsync(-1));
  public ICommand IncreaseMaxMessagesToMoveCommand => new AsyncRelayCommand(() => UpdateMaxMessagesToMoveAsync(1));
  public ICommand DecreaseDownloadSpeedLimitCommand => new AsyncRelayCommand(() => UpdateDownloadSpeedLimitAsync(-256));
  public ICommand IncreaseDownloadSpeedLimitCommand => new AsyncRelayCommand(() => UpdateDownloadSpeedLimitAsync(256));
  public ICommand DecreaseDownloadRetryCountCommand => new AsyncRelayCommand(() => UpdateDownloadRetryCountAsync(-1));
  public ICommand IncreaseDownloadRetryCountCommand => new AsyncRelayCommand(() => UpdateDownloadRetryCountAsync(1));
  public ICommand DecreaseDownloadRetryDelayCommand => new AsyncRelayCommand(() => UpdateDownloadRetryDelayAsync(-1));
  public ICommand IncreaseDownloadRetryDelayCommand => new AsyncRelayCommand(() => UpdateDownloadRetryDelayAsync(1));
  public ObservableCollection<LogEntryViewModel> RecentLogEntries { get; } = [];
  public ICollectionView RecentLogEntriesView => _recentLogEntriesView;
  public ObservableCollection<MailMessagePreviewViewModel> PreviewMessages { get; } = [];
  public ObservableCollection<FolderSelectionViewModel> AvailableFolders { get; } = [];
  public IReadOnlyList<string> LogLevelFilters { get; } = ["Tutti", "Information", "Warning", "Error"];
  public IReadOnlyList<SevenZipCompressionLevelOption> SevenZipCompressionLevels { get; } =
  [
    new("Veloce", 1),
    new("Normale", 3),
    SevenZipCompressionLevelOption.Default,
    new("Massima", 9)
  ];
  public string LogDirectory { get; }
  public string ReportDirectory => _operationReportService.ReportDirectory;
  public string DefaultDownloadDirectory => Path.Combine(ExecutableDirectory, "Downloads");
  public string ApplicationDirectory => _configuration.ApplicationDirectory;
  public string ConfigurationPath => _configuration.SettingsPath;
  public string ExecutableDirectory
  {
    get
    {
      var directory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
    }
  }
  public string AppVersion => CurrentVersion;
  public string WindowTitle => $"Carbonio Mail Archiver {CurrentVersion}";
  public string LicenseName => "MIT License";
  public string CopyrightText => "Copyright (c) 2026 Mauro Bettinelli";
  public string AppDescription => "Archiviazione e spostamento email Carbonio via API server";

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

  private string _selectedLogLevelFilter = "Tutti";
  public string SelectedLogLevelFilter
  {
    get => _selectedLogLevelFilter;
    set
    {
      if (string.Equals(_selectedLogLevelFilter, value, StringComparison.Ordinal))
      {
        return;
      }

      SetField(ref _selectedLogLevelFilter, value);
      _recentLogEntriesView.Refresh();
      StatusMessage = $"Filtro log: {SelectedLogLevelFilter}.";
    }
  }

  public sealed record LogEntryViewModel(
    string Timestamp,
    DateTimeOffset TimestampSortKey,
    string Level,
    string Source,
    string Message,
    string LevelSortKey,
    string SourceSortKey,
    string MessageSortKey);

  public string SearchBeforeDate
  {
    get => _searchBeforeDate;
    set
    {
      if (string.Equals(_searchBeforeDate, value, StringComparison.Ordinal))
      {
        return;
      }

      SetField(ref _searchBeforeDate, value);
      InvalidateDownloadVerification();
    }
  }

  public FolderSelectionViewModel? SelectedSourceFolder
  {
    get => _selectedSourceFolder;
    set
    {
      if (EqualityComparer<FolderSelectionViewModel?>.Default.Equals(_selectedSourceFolder, value))
      {
        return;
      }

      SetField(ref _selectedSourceFolder, value);
      if (value is not null)
      {
        _lastSourceFolderId = value.Id;
      }

      InvalidateDownloadVerification();
    }
  }

  public FolderSelectionViewModel? SelectedDestinationFolder
  {
    get => _selectedDestinationFolder;
    set
    {
      if (EqualityComparer<FolderSelectionViewModel?>.Default.Equals(_selectedDestinationFolder, value))
      {
        return;
      }

      SetField(ref _selectedDestinationFolder, value);
      if (value is not null)
      {
        _lastDestinationFolderId = value.Id;
      }

      InvalidateDownloadVerification();
    }
  }

  public bool UseArchiveDestination
  {
    get => _useArchiveDestination;
    set
    {
      if (EqualityComparer<bool>.Default.Equals(_useArchiveDestination, value))
      {
        return;
      }

      _useArchiveDestination = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseArchiveDestination)));
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDestinationFolderSelectionEnabled)));
      InvalidateDownloadVerification();
    }
  }

  public bool IsDestinationFolderSelectionEnabled => !UseArchiveDestination;

  public bool IncludeSourceSubfolders
  {
    get => _includeSourceSubfolders;
    set => SetField(ref _includeSourceSubfolders, value);
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

  public bool AutoLoadFoldersOnStartup
  {
    get => _autoLoadFoldersOnStartup;
    set => SetField(ref _autoLoadFoldersOnStartup, value);
  }

  public bool PromptReportExportAfterMove
  {
    get => _promptReportExportAfterMove;
    set => SetField(ref _promptReportExportAfterMove, value);
  }

  public string DownloadRootDirectory
  {
    get => _downloadRootDirectory;
    set
    {
      if (string.Equals(_downloadRootDirectory, value, StringComparison.Ordinal))
      {
        return;
      }

      SetField(ref _downloadRootDirectory, value);
      InvalidateDownloadVerification();
    }
  }

  public int TimeoutSeconds
  {
    get => _timeoutSeconds;
    set => SetField(ref _timeoutSeconds, value);
  }

  public int PreviewMessageLimit
  {
    get => _previewMessageLimit;
    set => SetField(ref _previewMessageLimit, Math.Clamp(value, 1, 100));
  }

  public int BatchSize
  {
    get => _batchSize;
    set => SetField(ref _batchSize, Math.Clamp(value, 10, 500));
  }

  public int MaxMessagesToMove
  {
    get => _maxMessagesToMove;
    set => SetField(ref _maxMessagesToMove, Math.Max(value, 0));
  }

  public int DownloadSpeedLimitKbps
  {
    get => _downloadSpeedLimitKbps;
    set => SetField(ref _downloadSpeedLimitKbps, Math.Clamp(value, 0, 10240));
  }

  public int DownloadRetryCount
  {
    get => _downloadRetryCount;
    set => SetField(ref _downloadRetryCount, Math.Clamp(value, 1, 10));
  }

  public int DownloadRetryDelaySeconds
  {
    get => _downloadRetryDelaySeconds;
    set => SetField(ref _downloadRetryDelaySeconds, Math.Clamp(value, 1, 300));
  }

  public SevenZipCompressionLevelOption SelectedSevenZipCompressionLevel
  {
    get => _selectedSevenZipCompressionLevel;
    set
    {
      var nextValue = value ?? SevenZipCompressionLevelOption.Default;
      if (EqualityComparer<SevenZipCompressionLevelOption>.Default.Equals(_selectedSevenZipCompressionLevel, nextValue))
      {
        return;
      }

      SetField(ref _selectedSevenZipCompressionLevel, nextValue);
      InvalidateDownloadVerification();
    }
  }

  public bool IsMoveInProgress
  {
    get => _isMoveInProgress;
    private set
    {
      SetField(ref _isMoveInProgress, value);
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOperationIdle)));
      _moveAllSearchResultsCommand.RaiseCanExecuteChanged();
      _cancelMoveCommand.RaiseCanExecuteChanged();
      if (DownloadMessagesCommand is AsyncRelayCommand downloadCommand)
      {
        downloadCommand.RaiseCanExecuteChanged();
      }

      if (VerifyDownloadedMessagesCommand is AsyncRelayCommand verifyCommand)
      {
        verifyCommand.RaiseCanExecuteChanged();
      }

      if (CompressMessagesCommand is AsyncRelayCommand compressCommand)
      {
        compressCommand.RaiseCanExecuteChanged();
      }
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompressMessages)));
    }
  }

  public bool IsOperationIdle => !IsMoveInProgress;

  public bool CanCompressMessages => !IsMoveInProgress && _downloadVerificationSucceeded;

  public int MoveProgressPercentage
  {
    get => _moveProgressPercentage;
    private set => SetField(ref _moveProgressPercentage, value);
  }

  public bool IsMoveProgressIndeterminate
  {
    get => _isMoveProgressIndeterminate;
    private set => SetField(ref _isMoveProgressIndeterminate, value);
  }

  public string MoveProgressText
  {
    get => _moveProgressText;
    private set => SetField(ref _moveProgressText, value);
  }

  public string MoveProgressPercentText
  {
    get => _moveProgressPercentText;
    private set => SetField(ref _moveProgressPercentText, value);
  }

  public string MoveBatchText
  {
    get => _moveBatchText;
    private set => SetField(ref _moveBatchText, value);
  }

  public string MoveDetailText
  {
    get => _moveDetailText;
    private set => SetField(ref _moveDetailText, value);
  }

  public string OperationMetricsText
  {
    get => _operationMetricsText;
    private set => SetField(ref _operationMetricsText, value);
  }

  public string OperationDownloadedText
  {
    get => _operationDownloadedText;
    private set => SetField(ref _operationDownloadedText, value);
  }

  public string OperationElapsedText
  {
    get => _operationElapsedText;
    private set => SetField(ref _operationElapsedText, value);
  }

  public string OperationSpeedText
  {
    get => _operationSpeedText;
    private set => SetField(ref _operationSpeedText, value);
  }

  public string OperationEtaText
  {
    get => _operationEtaText;
    private set => SetField(ref _operationEtaText, value);
  }

  public string OperationFolderText
  {
    get => _operationFolderText;
    private set => SetField(ref _operationFolderText, value);
  }

  public string OperationFileText
  {
    get => _operationFileText;
    private set => SetField(ref _operationFileText, value);
  }

  public string StatusMessage
  {
    get => _statusMessage;
    private set => SetField(ref _statusMessage, value);
  }

  public async Task InitializeAsync()
  {
    await LoadAsync();
    if (AutoLoadFoldersOnStartup && !string.IsNullOrEmpty(Password) && ValidateConnectionFields(ToSettings()) is null)
    {
      await LoadFoldersAsync();
    }

    await RefreshLogsAsync();
  }

  private async Task LoadAsync()
  {
    var settings = await _configuration.LoadConnectionSettingsAsync(CancellationToken.None);
    BaseUrl = settings.BaseUrl;
    SoapUrl = settings.SoapUrl;
    Email = settings.Email;
    _lastSourceFolderId = settings.LastSourceFolderId;
    _lastDestinationFolderId = settings.LastDestinationFolderId;
    RememberCredentials = settings.RememberCredentials;
    DiagnosticSoapLoggingEnabled = settings.DiagnosticSoapLoggingEnabled;
    AutoLoadFoldersOnStartup = settings.AutoLoadFoldersOnStartup;
    UseArchiveDestination = settings.UseArchiveDestination;
    IncludeSourceSubfolders = settings.IncludeSourceSubfolders;
    PromptReportExportAfterMove = settings.PromptReportExportAfterMove;
    TimeoutSeconds = settings.TimeoutSeconds;
    PreviewMessageLimit = Math.Clamp(settings.PreviewMessageLimit, 1, 100);
    BatchSize = Math.Clamp(settings.BatchSize, 10, 500);
    MaxMessagesToMove = Math.Max(settings.MaxMessagesToMove, 0);
    DownloadRootDirectory = string.IsNullOrWhiteSpace(settings.DownloadRootDirectory)
      ? DefaultDownloadDirectory
      : settings.DownloadRootDirectory;
    DownloadSpeedLimitKbps = Math.Clamp(settings.DownloadSpeedLimitKbps, 0, 10240);
    DownloadRetryCount = Math.Clamp(settings.DownloadRetryCount, 1, 10);
    DownloadRetryDelaySeconds = Math.Clamp(settings.DownloadRetryDelaySeconds, 1, 300);
    SelectedSevenZipCompressionLevel = SevenZipCompressionLevels.FirstOrDefault(option => option.Level == Math.Clamp(settings.SevenZipCompressionLevel, 0, 9))
      ?? SevenZipCompressionLevelOption.Default;
    if (TryNormalizeSavedSearchBeforeDate(settings.SearchBeforeDate, out var savedSearchBeforeDate))
    {
      SearchBeforeDate = savedSearchBeforeDate;
    }

    Password = settings.RememberCredentials ? await _credentialStore.ReadPasswordAsync(settings.Email, CancellationToken.None) ?? string.Empty : string.Empty;
    StatusMessage = settings.RememberCredentials && !string.IsNullOrEmpty(Password)
      ? "Configurazione caricata. Password protetta caricata da DPAPI."
      : "Configurazione caricata.";
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
    var sourceFolderLabel = SelectedSourceFolder?.AbsolutePath ?? settings.LastSourceFolderId;
    var destinationFolderLabel = settings.UseArchiveDestination
      ? "Archivio automatico"
      : SelectedDestinationFolder?.AbsolutePath ?? settings.LastDestinationFolderId;
    StatusMessage = $"Configurazione salvata. Cartelle: sorgente {sourceFolderLabel}, destinazione {destinationFolderLabel}. Le password non sono scritte nel JSON.";
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

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);

    StatusMessage = "Test connessione in corso...";
    var result = await _connectionDiagnosticService.TestConnectionAsync(settings, password, CancellationToken.None);
    StatusMessage = result.IsSuccess
      ? $"{result.Message} Account: {result.AccountName}. Versione: {result.ServerVersion ?? "non rilevata"}."
      : result.Message;
    await RefreshLogsAsync();
  }

  private async Task LoadFoldersAsync()
  {
    var settings = ToSettings();
    var validationError = ValidateConnectionFields(settings);
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);
    StatusMessage = "Caricamento cartelle in corso...";
    MoveProgressPercentage = 0;
    MoveProgressPercentText = string.Empty;
    IsMoveProgressIndeterminate = true;
    MoveBatchText = "Caricamento cartelle";
    MoveDetailText = "Lettura elenco cartelle dal server...";
    MoveProgressText = MoveDetailText;

    IReadOnlyDictionary<string, MailFolder> foldersById;
    try
    {
      foldersById = await _folderDiagnosticService.GetFoldersByIdAsync(settings, password, CancellationToken.None);
    }
    finally
    {
      IsMoveProgressIndeterminate = false;
    }

    AvailableFolders.Clear();

    foreach (var folder in foldersById.Values.OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase))
    {
      AvailableFolders.Add(new FolderSelectionViewModel(folder));
    }

    SelectedSourceFolder = AvailableFolders.FirstOrDefault(folder => folder.Id == _lastSourceFolderId)
      ?? AvailableFolders.FirstOrDefault(folder => folder.Id == "2")
      ?? AvailableFolders.FirstOrDefault();
    SelectedDestinationFolder = AvailableFolders.FirstOrDefault(folder => folder.Id == _lastDestinationFolderId && folder.Id != SelectedSourceFolder?.Id)
      ?? AvailableFolders.FirstOrDefault(folder => folder.Id != SelectedSourceFolder?.Id)
      ?? SelectedSourceFolder;
    StatusMessage = AvailableFolders.Count == 0
      ? "Nessuna cartella ricevuta dal server; la ricerca usera' Inbox."
      : $"Cartelle caricate: {AvailableFolders.Count}.";
    MoveBatchText = StatusMessage;
    MoveDetailText = "Elenco cartelle aggiornato.";
    MoveProgressText = MoveDetailText;
    await SaveSettingsSnapshotAsync();

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

    if (!TryParseSearchBeforeDate(out var beforeDate))
    {
      StatusMessage = "Data ricerca non valida. Usa formato gg/MM/aaaa.";
      return;
    }

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);
    var sourceFolderQuery = SelectedSourceFolder is null ? "in:inbox" : $"inid:{SelectedSourceFolder.Id}";
    var request = new MailSearchRequest(beforeDate, Math.Clamp(PreviewMessageLimit, 1, 100), sourceFolderQuery);
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

  private async Task SimulateMoveAsync()
  {
    var validationError = ValidateMovePreview();
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    var sourceFolder = SelectedSourceFolder!;
    var destinationFolder = GetPlannedDestinationFolder(sourceFolder);
    await SaveSettingsSnapshotAsync();
    _logger.LogInformation(
      "Simulazione spostamento: {Count} messaggi da {SourceFolder} ({SourceId}) a {DestinationFolder} ({DestinationId}).",
      PreviewMessages.Count,
      sourceFolder.AbsolutePath,
      sourceFolder.Id,
      destinationFolder.AbsolutePath,
      destinationFolder.Id);

    var archiveNote = UseArchiveDestination ? " Le cartelle mancanti sotto /Archive verrebbero create durante lo spostamento reale." : string.Empty;
    StatusMessage = $"Simulazione: {PreviewMessages.Count} messaggi verrebbero spostati da {sourceFolder.AbsolutePath} a {destinationFolder.AbsolutePath}.{archiveNote} Nessuna modifica eseguita.";
    await RefreshLogsAsync();
  }

  private async Task MovePreviewAsync()
  {
    var validationError = ValidateMovePreview();
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    var sourceFolder = SelectedSourceFolder!;
    var destinationFolder = GetPlannedDestinationFolder(sourceFolder);
    var confirmation = MessageBox.Show(
      $"Spostare realmente {PreviewMessages.Count} messaggi da {sourceFolder.AbsolutePath} a {destinationFolder.AbsolutePath}?",
      "Conferma spostamento",
      MessageBoxButton.YesNo,
      MessageBoxImage.Warning,
      MessageBoxResult.No);
    if (confirmation != MessageBoxResult.Yes)
    {
      StatusMessage = "Spostamento annullato.";
      return;
    }

    var settings = ToSettings();
    var validationSettingsError = ValidateConnectionFields(settings);
    if (validationSettingsError is not null)
    {
      StatusMessage = validationSettingsError;
      return;
    }

    var password = await GetPasswordAsync(settings);
    await SaveSettingsSnapshotAsync();
    var destinationResolve = await ResolveMoveDestinationAsync(settings, password, sourceFolder, CancellationToken.None);
    if (!destinationResolve.IsSuccess || destinationResolve.Folder is null)
    {
      StatusMessage = destinationResolve.Message;
      await RefreshLogsAsync();
      return;
    }

    destinationFolder = new FolderSelectionViewModel(destinationResolve.Folder);
    var messageIds = PreviewMessages.Select(message => message.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
    StatusMessage = "Spostamento reale in corso...";
    var result = await _moveDiagnosticService.MoveMessagesAsync(settings, password, messageIds, destinationFolder.Id, CancellationToken.None);
    if (!result.IsSuccess)
    {
      StatusMessage = $"Spostamento fallito. Richiesti: {result.RequestedCount}, spostati: {result.MovedCount}. {result.Fault?.Reason}";
      await RefreshLogsAsync();
      return;
    }

    PreviewMessages.Clear();
    StatusMessage = $"Spostamento completato. Messaggi spostati: {result.MovedCount}.";
    await RefreshLogsAsync();
    ShowMoveCompletedMessage();
  }

  private async Task MoveAllSearchResultsAsync()
  {
    if (IsMoveInProgress)
    {
      return;
    }

    var validationError = ValidateMoveSelection();
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    if (!TryParseSearchBeforeDate(out var beforeDate))
    {
      StatusMessage = "Data ricerca non valida. Usa formato gg/MM/aaaa.";
      return;
    }

    var settings = ToSettings();
    var validationSettingsError = ValidateConnectionFields(settings);
    if (validationSettingsError is not null)
    {
      StatusMessage = validationSettingsError;
      return;
    }

    var sourceFolder = SelectedSourceFolder!;
    var destinationFolder = GetPlannedDestinationFolder(sourceFolder);
    await SaveSettingsSnapshotAsync();
    var batchSize = Math.Clamp(BatchSize, 10, 500);
    var maxMessagesToMove = Math.Max(MaxMessagesToMove, 0);
    var password = await GetPasswordAsync(settings);
    using var moveCancellation = new CancellationTokenSource();
    _moveCancellationTokenSource = moveCancellation;
    IsMoveInProgress = true;
    MoveProgressPercentage = 0;
    IsMoveProgressIndeterminate = true;
    MoveProgressText = "Conteggio effettivo dei messaggi da spostare...";
    MoveProgressPercentText = string.Empty;
    MoveBatchText = "Conteggio";
    MoveDetailText = MoveProgressText;

    StatusMessage = MoveProgressText;
    BeginOperationMetrics();
    StartOperationMetricsTimer();
    IReadOnlyList<SourceFolderScan> folderScans;
    try
    {
      folderScans = await ScanSourceFoldersAsync(settings, password, sourceFolder, beforeDate, batchSize, maxMessagesToMove, moveCancellation.Token);
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Conteggio annullato dall'utente.";
      MoveProgressText = "Conteggio annullato.";
      MoveBatchText = "Conteggio annullato";
      MoveDetailText = MoveProgressText;
      ClearOperationMetrics();
      StopOperationMetricsTimer();
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    var failedScan = folderScans.FirstOrDefault(scan => !scan.IsSuccess);
    if (failedScan is not null)
    {
      StatusMessage = failedScan.Message;
      ClearOperationMetrics();
      StopOperationMetricsTimer();
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    var expectedTotal = folderScans.Sum(scan => scan.MessageIds.Count);
    if (expectedTotal == 0)
    {
      StatusMessage = "Nessun messaggio trovato da spostare.";
      PreviewMessages.Clear();
      ClearOperationMetrics();
      StopOperationMetricsTimer();
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    MoveProgressPercentage = 0;
    IsMoveProgressIndeterminate = false;
    MoveProgressPercentText = "0%";
    MoveBatchText = "Pronto";
    MoveProgressText = $"Pronto a spostare {expectedTotal} messaggi.";
    MoveDetailText = MoveProgressText;
    UpdateOperationMetrics(0, expectedTotal, string.Empty);
    var totalDescription = expectedTotal.ToString(CultureInfo.InvariantCulture);
    var limitDescription = maxMessagesToMove == 0
      ? "tutti i messaggi trovati"
      : $"massimo {maxMessagesToMove.ToString(CultureInfo.InvariantCulture)} messaggi";
    var folderDescription = folderScans.Count == 1
      ? sourceFolder.AbsolutePath
      : $"{sourceFolder.AbsolutePath} e {folderScans.Count - 1} sottocartelle";
    var confirmation = MessageBox.Show(
      $"Spostare realmente {totalDescription} messaggi da {folderDescription} a {destinationFolder.AbsolutePath}?\n\nData limite: prima del {beforeDate:dd/MM/yyyy}\nLimite richiesto: {limitDescription}\nBatch: {batchSize} messaggi per volta",
      "Conferma spostamento batch",
      MessageBoxButton.YesNo,
      MessageBoxImage.Warning,
      MessageBoxResult.No);
    if (confirmation != MessageBoxResult.Yes)
    {
      StatusMessage = "Spostamento batch annullato.";
      await RefreshLogsAsync();
      ResetMoveProgress();
      StopOperationMetricsTimer();
      return;
    }

    var operationStartedAt = DateTimeOffset.Now;
    var operationDestinationFolder = GetPlannedDestinationFolder(sourceFolder);
    var reportRows = folderScans.SelectMany(scan => scan.MessageIds)
      .Select(id => new MoveOperationReportRow(id, "Da spostare", null))
      .ToList();

    try
    {
      var movedCount = 0;
      var batchNumber = 0;
      var reportOffset = 0;

      foreach (var folderScan in folderScans)
      {
        moveCancellation.Token.ThrowIfCancellationRequested();

        var destinationResolve = await ResolveMoveDestinationAsync(settings, password, folderScan.SourceFolder, moveCancellation.Token);
        if (!destinationResolve.IsSuccess || destinationResolve.Folder is null)
        {
          foreach (var failedId in folderScan.MessageIds)
          {
            reportRows[reportOffset++] = new MoveOperationReportRow(failedId, "Errore", destinationResolve.Message);
          }

          var errorReportPath = await AskAndSaveMoveReportAsync(
            operationStartedAt,
            settings.Email,
            folderScan.SourceFolder,
            folderScan.PlannedDestinationFolder,
            beforeDate,
            batchSize,
            maxMessagesToMove,
            reportRows,
            "Interrotto per errore",
            CancellationToken.None);
      StatusMessage = $"Spostamento batch interrotto. Spostati: {movedCount}. Errore: {destinationResolve.Message}";
      StatusMessage += FormatReportStatus(errorReportPath);
      await RefreshLogsAsync();
      StopOperationMetricsTimer();
      return;
        }

        destinationFolder = new FolderSelectionViewModel(destinationResolve.Folder);
        foreach (var messageIdBatch in folderScan.MessageIds.Chunk(batchSize))
        {
          moveCancellation.Token.ThrowIfCancellationRequested();
          batchNumber++;
          UpdateMoveProgress(movedCount, expectedTotal, $"Batch {batchNumber} in corso", $"{folderScan.SourceFolder.AbsolutePath}: spostati finora {movedCount}/{expectedTotal} messaggi.");
          UpdateOperationMetrics(movedCount, expectedTotal, folderScan.SourceFolder.AbsolutePath);
          StatusMessage = $"{MoveBatchText}. {MoveDetailText}";
          var moveResult = await MoveMessagesWithRetryAsync(
            settings,
            password,
            messageIdBatch,
            destinationFolder.Id,
            settings.DownloadRetryCount,
            settings.DownloadRetryDelaySeconds,
            moveCancellation.Token);
          if (!moveResult.IsSuccess)
          {
            foreach (var failedId in messageIdBatch)
            {
              reportRows[reportOffset++] = new MoveOperationReportRow(failedId, "Errore", moveResult.Fault?.Reason);
            }

            var errorReportPath = await AskAndSaveMoveReportAsync(
              operationStartedAt,
              settings.Email,
              folderScan.SourceFolder,
              destinationFolder,
              beforeDate,
              batchSize,
              maxMessagesToMove,
              reportRows,
              "Interrotto per errore",
              CancellationToken.None);
      StatusMessage = $"Spostamento batch interrotto. Spostati: {movedCount}. Errore: {moveResult.Fault?.Reason}";
      StatusMessage += FormatReportStatus(errorReportPath);
      await RefreshLogsAsync();
      StopOperationMetricsTimer();
      return;
          }

          foreach (var movedId in messageIdBatch)
          {
            reportRows[reportOffset++] = new MoveOperationReportRow(movedId, "Spostato", null);
          }

          var previousMovedCount = movedCount;
          movedCount += moveResult.MovedCount;
          UpdateOperationMetrics(movedCount, expectedTotal, folderScan.SourceFolder.AbsolutePath);
          await AnimateMoveProgressAsync(previousMovedCount, movedCount, expectedTotal, $"Batch {batchNumber} completato", moveCancellation.Token);
          _logger.LogInformation(
            "Spostamento batch {BatchNumber} completato. Cartella: {SourceFolder}. Messaggi spostati nel batch: {BatchMoved}. Totale spostato: {MovedCount}.",
            batchNumber,
            folderScan.SourceFolder.AbsolutePath,
            moveResult.MovedCount,
            movedCount);
        }
      }

      PreviewMessages.Clear();
      UpdateMoveProgress(movedCount, movedCount, "Spostamento completato", $"{movedCount} messaggi spostati.");
      UpdateOperationMetrics(movedCount, movedCount, sourceFolder.AbsolutePath);
      var successReportPath = await AskAndSaveMoveReportAsync(
        operationStartedAt,
        settings.Email,
        sourceFolder,
        operationDestinationFolder,
        beforeDate,
        batchSize,
        maxMessagesToMove,
        reportRows,
        "Completato",
        CancellationToken.None);
      StatusMessage = $"Spostamento batch completato. Messaggi spostati: {movedCount}.{FormatReportStatus(successReportPath)}";
      await RefreshLogsAsync();
      ShowMoveCompletedMessage();
      StopOperationMetricsTimer();
    }
    catch (OperationCanceledException)
    {
      var reportPath = await AskAndSaveMoveReportAsync(
        operationStartedAt,
        settings.Email,
        sourceFolder,
        operationDestinationFolder,
        beforeDate,
        batchSize,
        maxMessagesToMove,
        reportRows.Select(row => row.Status == "Da spostare" ? row with { Status = "Non spostato", Detail = "Operazione annullata" } : row).ToList(),
        "Annullato",
        CancellationToken.None);
      StatusMessage = "Spostamento annullato dall'utente. L'eventuale batch gia' inviato potrebbe essere stato completato dal server.";
      StatusMessage += FormatReportStatus(reportPath);
      MoveProgressText = "Spostamento annullato.";
      MoveBatchText = "Spostamento annullato";
      MoveDetailText = "L'eventuale batch gia' inviato potrebbe essere stato completato dal server.";
      ClearOperationMetrics();
      StopOperationMetricsTimer();
      await RefreshLogsAsync();
    }
    finally
    {
      IsMoveInProgress = false;
      _moveCancellationTokenSource = null;
      StopOperationMetricsTimer();
    }
  }

  private Task CancelMoveAsync()
  {
    _moveCancellationTokenSource?.Cancel();
    StatusMessage = "Annullamento richiesto. Attendo il completamento dell'operazione corrente...";
    MoveProgressText = "Annullamento richiesto...";
    MoveBatchText = "Annullamento richiesto";
    MoveDetailText = "Attendo il completamento dell'operazione corrente...";
    IsMoveProgressIndeterminate = false;
    return Task.CompletedTask;
  }

  private async Task DownloadMessagesAsync()
  {
    if (IsMoveInProgress)
    {
      return;
    }

    var settings = ToSettings();
    var validationSettingsError = ValidateConnectionFields(settings);
    if (validationSettingsError is not null)
    {
      StatusMessage = validationSettingsError;
      return;
    }

    if (AvailableFolders.Count == 0)
    {
      await LoadFoldersAsync();
    }

    var rootFolder = ResolveDownloadRootFolder();
    if (rootFolder is null)
    {
      StatusMessage = UseArchiveDestination
        ? "Cartella /Archive non trovata. Carica le cartelle e verifica che Archivio sia attivo sul server."
        : "Seleziona una cartella destinazione da scaricare, oppure abilita Archivio.";
      return;
    }

    var foldersToDownload = GetFoldersUnder(rootFolder).ToArray();
    if (foldersToDownload.Length == 0)
    {
      StatusMessage = $"Nessuna cartella da scaricare per {rootFolder.AbsolutePath}.";
      return;
    }

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);
    var batchSize = Math.Clamp(BatchSize, 10, 500);
    var speedText = DownloadSpeedLimitKbps == 0
      ? "senza limite di velocita'"
      : $"{DownloadSpeedLimitKbps.ToString(CultureInfo.InvariantCulture)} KB/s";
    var confirmation = MessageBox.Show(
      $"Scaricare in formato EML la cartella {rootFolder.AbsolutePath} e le sue sottocartelle?\n\nCartelle da scandire: {foldersToDownload.Length}\nDestinazione locale: {settings.DownloadRootDirectory}\\{settings.Email}\nVelocita': {speedText}",
      "Conferma download EML",
      MessageBoxButton.YesNo,
      MessageBoxImage.Question,
      MessageBoxResult.No);
    if (confirmation != MessageBoxResult.Yes)
    {
      StatusMessage = "Download EML annullato.";
      return;
    }

    using var downloadCancellation = new CancellationTokenSource();
    _moveCancellationTokenSource = downloadCancellation;
    IsMoveInProgress = true;
    MoveProgressPercentage = 0;
    MoveProgressPercentText = string.Empty;
    MoveBatchText = string.Empty;
    MoveDetailText = $"Conteggio messaggi in {rootFolder.AbsolutePath}...";
    MoveProgressText = MoveDetailText;
    IsMoveProgressIndeterminate = true;
    OperationMetricsText = string.Empty;
    OperationDownloadedText = "MB scaricati: conteggio in corso";
    OperationElapsedText = "Trascorso: 0s";
    OperationSpeedText = "Velocita': in calcolo";
    OperationEtaText = "ETA: in calcolo";
    OperationFolderText = $"Cartella: {rootFolder.AbsolutePath}";
    OperationFileText = "File: conteggio in corso";
    _lastDownloadProgress = new MailDownloadProgress(rootFolder.AbsolutePath, "Conteggio messaggi...", 0, 0, 0, 0, 0, TimeSpan.Zero);
    _stableDownloadEtaText = "ETA in calcolo";
    _lastDownloadEtaUpdate = DateTimeOffset.MinValue;
    _downloadStartedAt = DateTimeOffset.Now;
    ResetDownloadWarmupThresholds();
    StartDownloadMetricsTimer();
    StatusMessage = MoveDetailText;

    var progress = new Progress<MailDownloadProgress>(downloadProgress =>
    {
      _lastDownloadProgress = downloadProgress;
      if (downloadProgress.TotalCount <= 0)
      {
        IsMoveProgressIndeterminate = true;
        MoveProgressPercentText = string.Empty;
        MoveProgressPercentage = 0;
        MoveDetailText = "Conteggio messaggi in corso...";
      }
      else
      {
        IsMoveProgressIndeterminate = false;
        MoveProgressPercentage = Math.Clamp((int)Math.Round(downloadProgress.CompletedCount * 100d / downloadProgress.TotalCount), 0, 100);
        MoveProgressPercentText = $"{MoveProgressPercentage}%";
        MoveDetailText = $"Messaggi completati: {downloadProgress.CompletedCount}/{downloadProgress.TotalCount}";
      }

      MoveBatchText = string.Empty;
      OperationFolderText = $"Cartella: {downloadProgress.CurrentFolder}";
      OperationFileText = $"File: {downloadProgress.CurrentFile}";
      UpdateDownloadMetricTexts(downloadProgress, GetMonotonicDownloadElapsed(downloadProgress.Elapsed), DateTimeOffset.Now);
      MoveProgressText = MoveDetailText;
      StatusMessage = MoveDetailText;
    });

    try
    {
      var result = await _messageDownloadService.DownloadFolderTreeAsync(
        settings,
        password,
        ToMailFolder(rootFolder),
        foldersToDownload.Select(ToMailFolder).ToArray(),
        settings.DownloadRootDirectory,
        batchSize,
        settings.DownloadSpeedLimitKbps,
        settings.DownloadRetryCount,
        settings.DownloadRetryDelaySeconds,
        progress,
        downloadCancellation.Token);

      if (!result.IsSuccess)
      {
        StatusMessage = result.Message;
        await RefreshLogsAsync();
        return;
      }

      MoveProgressPercentage = 100;
      MoveProgressPercentText = "100%";
      IsMoveProgressIndeterminate = false;
      MoveBatchText = "Download completato";
      MoveDetailText = $"{result.DownloadedCount} messaggi completati in {result.TargetDirectory}.";
      StopDownloadMetricsTimer();
      OperationMetricsText = string.Empty;
      OperationDownloadedText = string.Empty;
      OperationElapsedText = string.Empty;
      OperationSpeedText = string.Empty;
      OperationEtaText = string.Empty;
      OperationFolderText = string.Empty;
      OperationFileText = string.Empty;
      MoveProgressText = MoveDetailText;
      StatusMessage = result.Message;
      await RefreshLogsAsync();
      MessageBox.Show(
        "Download EML completato.",
        "Download completato",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Download EML annullato per {Account}.", settings.Email);
      StatusMessage = "Download EML annullato dall'utente.";
      StopDownloadMetricsTimer();
      ResetMoveProgress();
      await RefreshLogsAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or IOException or TaskCanceledException)
    {
      _logger.LogWarning(ex, "Download EML non completato per {Account}.", settings.Email);
      StatusMessage = $"Download EML non completato: {ex.Message}";
      StopDownloadMetricsTimer();
      OperationMetricsText = string.Empty;
      OperationDownloadedText = string.Empty;
      OperationElapsedText = string.Empty;
      OperationSpeedText = string.Empty;
      OperationEtaText = string.Empty;
      OperationFolderText = string.Empty;
      OperationFileText = string.Empty;
      await RefreshLogsAsync();
    }
    finally
    {
      StopDownloadMetricsTimer();
      IsMoveInProgress = false;
      _moveCancellationTokenSource = null;
    }
  }

  private async Task VerifyDownloadedMessagesAsync()
  {
    if (IsMoveInProgress)
    {
      return;
    }

    var settings = ToSettings();
    var validationSettingsError = ValidateConnectionFields(settings);
    if (validationSettingsError is not null)
    {
      StatusMessage = validationSettingsError;
      return;
    }

    if (AvailableFolders.Count == 0)
    {
      await LoadFoldersAsync();
    }

    var rootFolder = ResolveDownloadRootFolder();
    if (rootFolder is null)
    {
      StatusMessage = UseArchiveDestination
        ? "Cartella /Archive non trovata. Carica le cartelle e verifica che Archivio sia attivo sul server."
        : "Seleziona una cartella destinazione da verificare, oppure abilita Archivio.";
      return;
    }

    var foldersToVerify = GetFoldersUnder(rootFolder).ToArray();
    if (foldersToVerify.Length == 0)
    {
      StatusMessage = $"Nessuna cartella da verificare per {rootFolder.AbsolutePath}.";
      return;
    }

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);
    var batchSize = Math.Clamp(BatchSize, 10, 500);
    using var verifyCancellation = new CancellationTokenSource();
    _moveCancellationTokenSource = verifyCancellation;
    IsMoveInProgress = true;
    MoveProgressPercentage = 0;
    MoveProgressPercentText = string.Empty;
    MoveBatchText = string.Empty;
    MoveDetailText = $"Verifica EML in {rootFolder.AbsolutePath}...";
    MoveProgressText = MoveDetailText;
    IsMoveProgressIndeterminate = true;
    OperationMetricsText = string.Empty;
    OperationDownloadedText = string.Empty;
    OperationElapsedText = string.Empty;
    OperationSpeedText = string.Empty;
    OperationEtaText = string.Empty;
    OperationFolderText = $"Cartella: {rootFolder.AbsolutePath}";
    OperationFileText = "Verifica in corso";
    StatusMessage = MoveDetailText;
    BeginOperationMetrics();

    var progress = new Progress<MailDownloadProgress>(verifyProgress =>
    {
      OperationFolderText = $"Cartella: {verifyProgress.CurrentFolder}";
      OperationFileText = verifyProgress.CurrentFile;
      MoveDetailText = verifyProgress.TotalCount <= 0
        ? "Verifica EML: conteggio messaggi in corso..."
        : $"Verifica EML: presenti {verifyProgress.CompletedCount}/{verifyProgress.TotalCount}.";
      MoveProgressText = MoveDetailText;
      UpdateOperationMetrics(verifyProgress.CompletedCount, verifyProgress.TotalCount, verifyProgress.CurrentFolder);
      StatusMessage = MoveDetailText;
    });

    try
    {
      var result = await _messageDownloadService.VerifyFolderTreeAsync(
        settings,
        password,
        ToMailFolder(rootFolder),
        foldersToVerify.Select(ToMailFolder).ToArray(),
        settings.DownloadRootDirectory,
        batchSize,
        progress,
        verifyCancellation.Token);

      IsMoveProgressIndeterminate = false;
      MoveProgressPercentage = result.ExpectedCount == 0
        ? 0
        : Math.Clamp((int)Math.Round(result.PresentCount * 100d / result.ExpectedCount), 0, 100);
      MoveProgressPercentText = result.ExpectedCount == 0 ? string.Empty : $"{MoveProgressPercentage}%";
      MoveDetailText = result.Message;
      MoveProgressText = MoveDetailText;
      OperationFolderText = $"Cartella: {rootFolder.AbsolutePath}";
      OperationFileText = $"Percorso locale: {result.TargetDirectory}";
      StatusMessage = result.Message;
      UpdateOperationMetrics(result.PresentCount, result.ExpectedCount, rootFolder.AbsolutePath);
      _downloadVerificationSucceeded = result.IsSuccess && result.MissingCount == 0;
      if (CompressMessagesCommand is AsyncRelayCommand compressCommand)
      {
        compressCommand.RaiseCanExecuteChanged();
      }
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompressMessages)));
      await RefreshLogsAsync();
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Verifica EML annullata dall'utente.";
      _downloadVerificationSucceeded = false;
      ClearOperationMetrics();
      if (CompressMessagesCommand is AsyncRelayCommand compressCommand)
      {
        compressCommand.RaiseCanExecuteChanged();
      }
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompressMessages)));
      ResetMoveProgress();
      await RefreshLogsAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or IOException or TaskCanceledException)
    {
      StatusMessage = $"Verifica EML non completata: {ex.Message}";
      IsMoveProgressIndeterminate = false;
      _downloadVerificationSucceeded = false;
      ClearOperationMetrics();
      if (CompressMessagesCommand is AsyncRelayCommand compressCommand)
      {
        compressCommand.RaiseCanExecuteChanged();
      }
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompressMessages)));
      await RefreshLogsAsync();
    }
    finally
    {
      IsMoveInProgress = false;
      _moveCancellationTokenSource = null;
    }
  }

  private void InvalidateDownloadVerification()
  {
    if (!_downloadVerificationSucceeded)
    {
      return;
    }

    _downloadVerificationSucceeded = false;
    if (CompressMessagesCommand is AsyncRelayCommand compressCommand)
    {
      compressCommand.RaiseCanExecuteChanged();
    }
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompressMessages)));
  }

  private async Task CompressMessagesAsync()
  {
    if (IsMoveInProgress)
    {
      return;
    }

    var settings = ToSettings();
    var validationSettingsError = ValidateConnectionFields(settings);
    if (validationSettingsError is not null)
    {
      StatusMessage = validationSettingsError;
      return;
    }

    if (AvailableFolders.Count == 0)
    {
      await LoadFoldersAsync();
    }

    var rootFolder = ResolveDownloadRootFolder();
    if (rootFolder is null)
    {
      StatusMessage = UseArchiveDestination
        ? "Cartella /Archive non trovata. Carica le cartelle e verifica che Archivio sia attivo sul server."
        : "Seleziona una cartella destinazione da comprimere, oppure abilita Archivio.";
      return;
    }

    var foldersToVerify = GetFoldersUnder(rootFolder).ToArray();
    if (foldersToVerify.Length == 0)
    {
      StatusMessage = $"Nessuna cartella da verificare per {rootFolder.AbsolutePath}.";
      return;
    }

    var accountDownloadDirectory = Path.Combine(settings.DownloadRootDirectory, SanitizePathSegment(settings.Email));
    if (!Directory.Exists(accountDownloadDirectory))
    {
      StatusMessage = $"Cartella download della casella non trovata: {accountDownloadDirectory}. Esegui prima Scarica EML.";
      return;
    }

    var archivePath = Path.Combine(settings.DownloadRootDirectory, $"{SanitizePathSegment(settings.Email)}.7z");
    var confirmation = MessageBox.Show(
      $"Verificare e comprimere la cartella download della casella?\n\nCartella: {accountDownloadDirectory}\nArchivio: {archivePath}\n\nSe la compressione va a buon fine, la cartella non compressa verra' eliminata.",
      "Conferma compressione EML",
      MessageBoxButton.YesNo,
      MessageBoxImage.Question,
      MessageBoxResult.No);
    if (confirmation != MessageBoxResult.Yes)
    {
      StatusMessage = "Compressione EML annullata.";
      return;
    }

    await SaveSettingsSnapshotAsync();
    var password = await GetPasswordAsync(settings);
    var batchSize = Math.Clamp(BatchSize, 10, 500);
    using var compressionCancellation = new CancellationTokenSource();
    _moveCancellationTokenSource = compressionCancellation;
    IsMoveInProgress = true;
    MoveProgressPercentage = 0;
    MoveProgressPercentText = string.Empty;
    MoveBatchText = string.Empty;
    MoveDetailText = $"Verifica EML in {rootFolder.AbsolutePath}...";
    MoveProgressText = MoveDetailText;
    IsMoveProgressIndeterminate = true;
    OperationMetricsText = string.Empty;
    OperationDownloadedText = string.Empty;
    OperationElapsedText = string.Empty;
    OperationSpeedText = string.Empty;
    OperationEtaText = string.Empty;
    OperationFolderText = $"Cartella: {rootFolder.AbsolutePath}";
    OperationFileText = "Verifica in corso";
    StatusMessage = MoveDetailText;
    BeginOperationMetrics();

    var verifyProgress = new Progress<MailDownloadProgress>(downloadProgress =>
    {
      OperationFolderText = $"Cartella: {downloadProgress.CurrentFolder}";
      OperationFileText = downloadProgress.CurrentFile;
      MoveDetailText = downloadProgress.TotalCount <= 0
        ? "Verifica EML: conteggio messaggi in corso..."
        : $"Verifica EML: presenti {downloadProgress.CompletedCount}/{downloadProgress.TotalCount}.";
      MoveProgressText = MoveDetailText;
      StatusMessage = MoveDetailText;
    });

    try
    {
      var verification = await _messageDownloadService.VerifyFolderTreeAsync(
        settings,
        password,
        ToMailFolder(rootFolder),
        foldersToVerify.Select(ToMailFolder).ToArray(),
        settings.DownloadRootDirectory,
        batchSize,
        verifyProgress,
        compressionCancellation.Token);

      if (!verification.IsSuccess || verification.MissingCount > 0)
      {
        IsMoveProgressIndeterminate = false;
        MoveProgressPercentage = verification.ExpectedCount == 0
          ? 0
          : Math.Clamp((int)Math.Round(verification.PresentCount * 100d / verification.ExpectedCount), 0, 100);
        MoveProgressPercentText = verification.ExpectedCount == 0 ? string.Empty : $"{MoveProgressPercentage}%";
        MoveDetailText = $"Compressione interrotta: verifica EML non completa. {verification.Message}";
        MoveProgressText = MoveDetailText;
        StatusMessage = MoveDetailText;
        ClearOperationMetrics();
        await RefreshLogsAsync();
        return;
      }

      IsMoveProgressIndeterminate = true;
      MoveProgressPercentText = string.Empty;
      MoveDetailText = $"Compressione 7z in corso da {accountDownloadDirectory}...";
      MoveProgressText = MoveDetailText;
      OperationFolderText = $"Cartella: {accountDownloadDirectory}";
      OperationFileText = "Preparazione archivio";
      StatusMessage = MoveDetailText;

      var archiveProgress = new Progress<string>(entryName =>
      {
        OperationFileText = $"File: {entryName}";
        StatusMessage = "Compressione 7z in corso...";
      });

      var createdArchivePath = await _archiveExportService.CreateSevenZipAsync(
        accountDownloadDirectory,
        archivePath,
        settings.SevenZipCompressionLevel,
        archiveProgress,
        compressionCancellation.Token);

      compressionCancellation.Token.ThrowIfCancellationRequested();
      if (!File.Exists(createdArchivePath))
      {
        throw new IOException($"Archivio 7z non trovato dopo la creazione: {createdArchivePath}");
      }

      OperationFileText = "Eliminazione cartella non compressa";
      await Task.Run(
        () =>
        {
          compressionCancellation.Token.ThrowIfCancellationRequested();
          Directory.Delete(accountDownloadDirectory, true);
        },
        compressionCancellation.Token);

      IsMoveProgressIndeterminate = false;
      MoveProgressPercentage = 100;
      MoveProgressPercentText = "100%";
      MoveBatchText = "Compressione completata";
      MoveDetailText = $"Archivio 7z creato: {createdArchivePath}. Cartella non compressa eliminata.";
      MoveProgressText = MoveDetailText;
      OperationFolderText = $"Archivio: {createdArchivePath}";
      OperationFileText = string.Empty;
      StatusMessage = MoveDetailText;
      ClearOperationMetrics();
      await RefreshLogsAsync();
      MessageBox.Show(
        "Compressione EML completata.",
        "Compressione completata",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Compressione EML annullata dall'utente.";
      ResetMoveProgress();
      ClearOperationMetrics();
      await RefreshLogsAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or IOException or UnauthorizedAccessException or TaskCanceledException)
    {
      _logger.LogWarning(ex, "Compressione EML non completata per {Account}.", settings.Email);
      StatusMessage = $"Compressione EML non completata: {ex.Message}";
      IsMoveProgressIndeterminate = false;
      ClearOperationMetrics();
      await RefreshLogsAsync();
    }
    finally
    {
      IsMoveInProgress = false;
      _moveCancellationTokenSource = null;
    }
  }

  private async Task<IReadOnlyList<SourceFolderScan>> ScanSourceFoldersAsync(
    CarbonioConnectionSettings settings,
    string password,
    FolderSelectionViewModel sourceFolder,
    DateOnly beforeDate,
    int batchSize,
    int maxMessagesToMove,
    CancellationToken cancellationToken)
  {
    var foldersToProcess = GetSourceFoldersToProcess(sourceFolder);
    var scans = new List<SourceFolderScan>();
    var remainingLimit = maxMessagesToMove;

    foreach (var folder in foldersToProcess)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (maxMessagesToMove > 0 && remainingLimit <= 0)
      {
        break;
      }

      var folderLimit = maxMessagesToMove == 0 ? 0 : remainingLimit;
      MoveBatchText = "Conteggio";
      MoveDetailText = $"Conteggio {folder.AbsolutePath}...";
      MoveProgressText = MoveDetailText;
      StatusMessage = MoveDetailText;

      var scanResult = await ScanMessageIdsAsync(
        settings,
        password,
        beforeDate,
        $"inid:{folder.Id}",
        batchSize,
        folderLimit,
        folder.AbsolutePath,
        cancellationToken);

      scans.Add(new SourceFolderScan(
        folder,
        GetPlannedDestinationFolder(folder),
        scanResult.IsSuccess,
        scanResult.Message,
        scanResult.MessageIds));

      if (!scanResult.IsSuccess)
      {
        break;
      }

      if (maxMessagesToMove > 0)
      {
        remainingLimit -= scanResult.MessageIds.Count;
      }
    }

    return scans;
  }

  private IReadOnlyList<FolderSelectionViewModel> GetSourceFoldersToProcess(FolderSelectionViewModel sourceFolder)
  {
    if (!IncludeSourceSubfolders)
    {
      return [sourceFolder];
    }

    var sourcePrefix = sourceFolder.AbsolutePath.TrimEnd('/') + "/";
    return AvailableFolders
      .Where(folder => folder.Id == sourceFolder.Id || folder.AbsolutePath.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
      .OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase)
      .ToArray();
  }

  private FolderSelectionViewModel? ResolveDownloadRootFolder()
  {
    if (UseArchiveDestination)
    {
      return AvailableFolders.FirstOrDefault(folder => string.Equals(folder.AbsolutePath, "/Archive", StringComparison.OrdinalIgnoreCase));
    }

    return SelectedDestinationFolder;
  }

  private IEnumerable<FolderSelectionViewModel> GetFoldersUnder(FolderSelectionViewModel rootFolder)
  {
    var rootPrefix = rootFolder.AbsolutePath.TrimEnd('/') + "/";
    return AvailableFolders
      .Where(folder => folder.Id == rootFolder.Id || folder.AbsolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
      .OrderBy(folder => folder.AbsolutePath, StringComparer.CurrentCultureIgnoreCase);
  }

  private async Task<(bool IsSuccess, string Message, IReadOnlyList<string> MessageIds)> ScanMessageIdsAsync(
    CarbonioConnectionSettings settings,
    string password,
    DateOnly beforeDate,
    string sourceFolderQuery,
    int batchSize,
    int maxMessagesToMove,
    string sourceFolderLabel,
    CancellationToken cancellationToken)
  {
    var messageIds = new List<string>();
    var knownIds = new HashSet<string>(StringComparer.Ordinal);
    var offset = 0;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      MoveProgressText = maxMessagesToMove == 0
        ? $"Scansione {sourceFolderLabel}: {messageIds.Count} messaggi trovati..."
        : $"Scansione {sourceFolderLabel}: {messageIds.Count}/{maxMessagesToMove} messaggi selezionati...";
      MoveBatchText = $"Scansione cartella";
      MoveDetailText = MoveProgressText;
      StatusMessage = MoveProgressText;

      var request = new MailSearchRequest(beforeDate, batchSize, sourceFolderQuery, offset);
      var page = await _searchDiagnosticService.SearchInboxBeforeAsync(settings, password, request, cancellationToken);
      if (!page.IsSuccess)
      {
        return (false, page.Message, messageIds);
      }

      var remainingLimit = maxMessagesToMove == 0 ? int.MaxValue : maxMessagesToMove - messageIds.Count;
      var newIds = page.Messages
        .Select(message => message.Id)
        .Where(id => !string.IsNullOrWhiteSpace(id) && knownIds.Add(id))
        .Take(remainingLimit)
        .ToArray();
      messageIds.AddRange(newIds);

      if (maxMessagesToMove > 0 && messageIds.Count >= maxMessagesToMove)
      {
        return (true, $"Conteggio completato. Messaggi selezionati: {messageIds.Count}.", messageIds);
      }

      if (!page.HasMore || page.Messages.Count < batchSize || newIds.Length == 0)
      {
        return (true, $"Conteggio completato. Messaggi trovati: {messageIds.Count}.", messageIds);
      }

      offset += batchSize;
    }
  }

  private Task<string> SaveMoveReportAsync(
    DateTimeOffset startedAt,
    string account,
    FolderSelectionViewModel sourceFolder,
    FolderSelectionViewModel destinationFolder,
    DateOnly beforeDate,
    int batchSize,
    int requestedLimit,
    IReadOnlyList<MoveOperationReportRow> rows,
    string result,
    CancellationToken cancellationToken)
  {
    var report = new MoveOperationReport(
      startedAt,
      DateTimeOffset.Now,
      account,
      sourceFolder.AbsolutePath,
      sourceFolder.Id,
      destinationFolder.AbsolutePath,
      destinationFolder.Id,
      beforeDate,
      batchSize,
      requestedLimit,
      rows,
      result);
    return _operationReportService.ExportMoveReportAsync(report, cancellationToken);
  }

  private async Task<string?> AskAndSaveMoveReportAsync(
    DateTimeOffset startedAt,
    string account,
    FolderSelectionViewModel sourceFolder,
    FolderSelectionViewModel destinationFolder,
    DateOnly beforeDate,
    int batchSize,
    int requestedLimit,
    IReadOnlyList<MoveOperationReportRow> rows,
    string result,
    CancellationToken cancellationToken)
  {
    if (!PromptReportExportAfterMove)
    {
      return null;
    }

    var confirmation = MessageBox.Show(
      "Esportare un report CSV dell'operazione?",
      "Report operazione",
      MessageBoxButton.YesNo,
      MessageBoxImage.Question,
      MessageBoxResult.Yes);
    if (confirmation != MessageBoxResult.Yes)
    {
      return null;
    }

    var path = await SaveMoveReportAsync(
      startedAt,
      account,
      sourceFolder,
      destinationFolder,
      beforeDate,
      batchSize,
      requestedLimit,
      rows,
      result,
      cancellationToken);
    _lastReportPath = path;
    return path;
  }

  private static string FormatReportStatus(string? reportPath)
  {
    return string.IsNullOrWhiteSpace(reportPath)
      ? " Report non esportato."
      : $" Report: {reportPath}";
  }

  private static void ShowMoveCompletedMessage()
  {
    MessageBox.Show(
      "Spostamento completato.",
      "Spostamento completato",
      MessageBoxButton.OK,
      MessageBoxImage.Information);
  }

  private async Task RefreshLogsAsync()
  {
    RecentLogEntries.Clear();
    var lines = await _operationLogService.ReadRecentLinesAsync(200, CancellationToken.None);
    foreach (var line in lines)
    {
      var entry = ParseLogLine(line);
      if (entry is not null)
      {
        RecentLogEntries.Add(entry);
      }
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

  private async Task ClearLogsAsync()
  {
    await _operationLogService.ClearAsync(CancellationToken.None);
    RecentLogEntries.Clear();
    RecentLogText = string.Empty;
    StatusMessage = "Log cancellato.";
  }

  private static LogEntryViewModel? ParseLogLine(string line)
  {
    var parts = line.Split('\t', 5, StringSplitOptions.None);
    if (parts.Length < 4)
    {
      return null;
    }

    var timestamp = FormatLogTimestamp(parts[0]);
    var timestampSortKey = DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedTimestamp)
      ? parsedTimestamp
      : DateTimeOffset.MinValue;
    var level = parts[1].Trim();
    var source = FormatLogSource(parts[2].Trim());
    var message = FormatLogMessage(parts[3].Trim(), parts.Length >= 5 ? parts[4].Trim() : string.Empty, source);
    return new LogEntryViewModel(
      timestamp,
      timestampSortKey,
      level,
      source,
      message,
      level,
      source,
      message);
  }

  private static string FormatLogTimestamp(string timestamp)
  {
    return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
      ? parsed.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture)
      : timestamp;
  }

  private static string FormatLogSource(string source)
  {
    if (string.IsNullOrWhiteSpace(source))
    {
      return "Sistema";
    }

    return source switch
    {
      "Microsoft.Hosting.Lifetime" => "App",
      "CarbonioMailArchiver.App.ViewModels.MainWindowViewModel" => "Interfaccia",
      _ when source.EndsWith("DiagnosticService", StringComparison.Ordinal) => source.Replace("CarbonioMailArchiver.Infrastructure.Services.", string.Empty, StringComparison.Ordinal),
      _ => source.Replace("CarbonioMailArchiver.Infrastructure.Services.", string.Empty, StringComparison.Ordinal)
    };
  }

  private static string FormatLogMessage(string message, string extra, string source)
  {
    var cleanMessage = message.Trim();
    var cleanExtra = extra.Trim();

    if (source == "App")
    {
      return cleanMessage switch
      {
        "Application started. Press Ctrl+C to shut down." => "Applicazione avviata.",
        "Application is shutting down..." => "Applicazione in chiusura.",
        _ => cleanMessage
      };
    }

    if (!string.IsNullOrWhiteSpace(cleanExtra))
    {
      cleanMessage = $"{cleanMessage} - {cleanExtra}";
    }

    return cleanMessage;
  }

  private bool FilterLogEntry(object item)
  {
    if (item is not LogEntryViewModel entry)
    {
      return false;
    }

    return SelectedLogLevelFilter switch
    {
      "Information" => string.Equals(entry.Level, "Information", StringComparison.OrdinalIgnoreCase),
      "Warning" => string.Equals(entry.Level, "Warning", StringComparison.OrdinalIgnoreCase),
      "Error" => string.Equals(entry.Level, "Error", StringComparison.OrdinalIgnoreCase),
      _ => true
    };
  }

  private async Task DeleteSelectedFolderIfEmptyAsync(FolderSelectionViewModel? folder, string role)
  {
    if (folder is null)
    {
      StatusMessage = $"Seleziona una cartella {role}.";
      return;
    }

    var settings = ToSettings();
    var validationError = ValidateConnectionFields(settings);
    if (validationError is not null)
    {
      StatusMessage = validationError;
      return;
    }

    var password = await GetPasswordAsync(settings);
    StatusMessage = $"Verifica cartella vuota: {folder.AbsolutePath}...";
    try
    {
      var plan = await _folderMaintenanceService.AnalyzeEmptyFoldersAsync(settings, password, folder.Id, IncludeSourceSubfolders, CancellationToken.None);
      if (!plan.IsSuccess || plan.CandidatePaths.Count == 0)
      {
        StatusMessage = plan.Message;
        await RefreshLogsAsync();
        return;
      }

      PreviewMessages.Clear();
      foreach (var candidatePath in plan.CandidatePaths)
      {
        PreviewMessages.Add(new MailMessagePreviewViewModel(candidatePath, "Cartella vuota candidata allo spostamento nel cestino", candidatePath));
      }
      StatusMessage = $"Cartelle vuote candidate: {plan.CandidatePaths.Count}. Controlla la preview prima di confermare.";

      var confirmation = await ShowFolderDeleteConfirmationAsync(plan.CandidatePaths.Count, IncludeSourceSubfolders);
      if (confirmation != true)
      {
        StatusMessage = "Spostamento cartelle vuote nel cestino annullato.";
        return;
      }

      var result = await _folderMaintenanceService.TrashEmptyFoldersAsync(settings, password, folder.Id, IncludeSourceSubfolders, CancellationToken.None);
      StatusMessage = result.Message;
      await LoadFoldersAsync();
      await RefreshLogsAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
    {
      StatusMessage = $"Spostamento cartella nel cestino non completato: {ex.Message}";
      await RefreshLogsAsync();
    }
  }

  private Task<bool?> ShowFolderDeleteConfirmationAsync(int candidateCount, bool includeSubfolders)
  {
    var scope = includeSubfolders ? "cartelle vuote trovate nel ramo selezionato" : "cartella selezionata";
    var summary = $"Spostare nel cestino {candidateCount} {scope}?";
    var detail = "L'elenco completo resta visibile nella preview della finestra principale.";

    var tcs = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
    Application.Current.Dispatcher.InvokeAsync(() =>
    {
      var owner = Application.Current.MainWindow;
      var window = new FolderDeleteConfirmationWindow(summary, detail)
      {
        Owner = owner
      };
      window.Closed += (_, _) => tcs.TrySetResult(window.DialogResult);
      window.Show();
    });

    return tcs.Task;
  }

  private Task UpdatePreviewMessageLimitAsync(int delta)
  {
    PreviewMessageLimit = Math.Clamp(PreviewMessageLimit + delta, 1, 100);
    return Task.CompletedTask;
  }

  private Task UpdateBatchSizeAsync(int delta)
  {
    BatchSize = Math.Clamp(BatchSize + delta, 10, 500);
    return Task.CompletedTask;
  }

  private Task UpdateMaxMessagesToMoveAsync(int delta)
  {
    MaxMessagesToMove = Math.Max(MaxMessagesToMove + delta, 0);
    return Task.CompletedTask;
  }

  private Task UpdateDownloadSpeedLimitAsync(int delta)
  {
    DownloadSpeedLimitKbps = Math.Clamp(DownloadSpeedLimitKbps + delta, 0, 10240);
    return Task.CompletedTask;
  }

  private Task UpdateDownloadRetryCountAsync(int delta)
  {
    DownloadRetryCount = Math.Clamp(DownloadRetryCount + delta, 1, 10);
    return Task.CompletedTask;
  }

  private Task UpdateDownloadRetryDelayAsync(int delta)
  {
    DownloadRetryDelaySeconds = Math.Clamp(DownloadRetryDelaySeconds + delta, 1, 300);
    return Task.CompletedTask;
  }

  private Task RestoreConfigurationDefaultsAsync()
  {
    TimeoutSeconds = 100;
    PreviewMessageLimit = 10;
    BatchSize = 250;
    MaxMessagesToMove = 0;
    AutoLoadFoldersOnStartup = false;
    UseArchiveDestination = false;
    IncludeSourceSubfolders = false;
    DiagnosticSoapLoggingEnabled = false;
    PromptReportExportAfterMove = true;
    DownloadRootDirectory = DefaultDownloadDirectory;
    DownloadSpeedLimitKbps = 0;
    DownloadRetryCount = 3;
    DownloadRetryDelaySeconds = 10;
    SelectedSevenZipCompressionLevel = SevenZipCompressionLevelOption.Default;
    StatusMessage = "Default configurazione ripristinati. Premi Salva configurazione per renderli permanenti.";
    return Task.CompletedTask;
  }

  private Task BrowseDownloadRootDirectoryAsync()
  {
    var dialog = new OpenFolderDialog
    {
      Title = "Seleziona cartella download EML",
      Multiselect = false,
      InitialDirectory = Directory.Exists(DownloadRootDirectory) ? DownloadRootDirectory : DefaultDownloadDirectory
    };

    if (dialog.ShowDialog() == true)
    {
      DownloadRootDirectory = dialog.FolderName;
      StatusMessage = $"Cartella download EML selezionata: {DownloadRootDirectory}";
    }

    return Task.CompletedTask;
  }

  private Task OpenLicenseAsync()
  {
    var licensePath = Path.Combine(AppContext.BaseDirectory, "LICENSE");
    if (!File.Exists(licensePath))
    {
      licensePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LICENSE"));
    }

    return OpenPathAsync(File.Exists(licensePath) ? licensePath : RepositoryUrl);
  }

  private Task OpenLastReportAsync()
  {
    if (!string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath))
    {
      return OpenPathAsync(_lastReportPath);
    }

    var lastReport = Directory
      .EnumerateFiles(ReportDirectory, "move-report-*.csv")
      .OrderByDescending(File.GetLastWriteTimeUtc)
      .FirstOrDefault();
    return OpenPathAsync(lastReport ?? ReportDirectory);
  }

  private Task OpenPathAsync(string pathOrUrl)
  {
    try
    {
      if (!pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
      {
        var directory = Path.HasExtension(pathOrUrl) ? Path.GetDirectoryName(pathOrUrl) : pathOrUrl;
        if (!string.IsNullOrWhiteSpace(directory))
        {
          Directory.CreateDirectory(directory);
        }
      }

      Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
      StatusMessage = $"Impossibile aprire: {pathOrUrl}. {ex.Message}";
    }

    return Task.CompletedTask;
  }

  private string GetEffectiveDownloadRootDirectory()
  {
    return string.IsNullOrWhiteSpace(DownloadRootDirectory) ? DefaultDownloadDirectory : DownloadRootDirectory.Trim();
  }

  private static string SanitizePathSegment(string value)
  {
    var invalidChars = Path.GetInvalidFileNameChars();
    var sanitized = new string(value
      .Select(character => invalidChars.Contains(character) ? '_' : character)
      .ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized.Trim();
  }

  private CarbonioConnectionSettings ToSettings()
  {
    return new CarbonioConnectionSettings
    {
      BaseUrl = BaseUrl.Trim(),
      SoapUrl = SoapUrl.Trim(),
      Email = Email.Trim(),
      LastSourceFolderId = SelectedSourceFolder?.Id ?? _lastSourceFolderId,
      LastDestinationFolderId = SelectedDestinationFolder?.Id ?? _lastDestinationFolderId,
      UseArchiveDestination = UseArchiveDestination,
      IncludeSourceSubfolders = IncludeSourceSubfolders,
      RememberCredentials = RememberCredentials,
      AcceptUntrustedCertificates = false,
      DiagnosticSoapLoggingEnabled = DiagnosticSoapLoggingEnabled,
      AutoLoadFoldersOnStartup = AutoLoadFoldersOnStartup,
      TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600),
      PreviewMessageLimit = Math.Clamp(PreviewMessageLimit, 1, 100),
      BatchSize = Math.Clamp(BatchSize, 10, 500),
      MaxMessagesToMove = Math.Max(MaxMessagesToMove, 0),
      PromptReportExportAfterMove = PromptReportExportAfterMove,
      DownloadRootDirectory = string.IsNullOrWhiteSpace(DownloadRootDirectory) ? DefaultDownloadDirectory : DownloadRootDirectory.Trim(),
      DownloadSpeedLimitKbps = Math.Clamp(DownloadSpeedLimitKbps, 0, 10240),
      DownloadRetryCount = Math.Clamp(DownloadRetryCount, 1, 10),
      DownloadRetryDelaySeconds = Math.Clamp(DownloadRetryDelaySeconds, 1, 300),
      SevenZipCompressionLevel = Math.Clamp(SelectedSevenZipCompressionLevel.Level, 0, 9),
      SearchBeforeDate = TryParseSearchBeforeDate(out var beforeDate) ? beforeDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : SearchBeforeDate.Trim()
    };
  }

  private Task SaveSettingsSnapshotAsync()
  {
    return _configuration.SaveConnectionSettingsAsync(ToSettings(), CancellationToken.None);
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

  private bool TryParseSearchBeforeDate(out DateOnly beforeDate)
  {
    return DateOnly.TryParseExact(
      SearchBeforeDate.Trim(),
      "dd/MM/yyyy",
      CultureInfo.InvariantCulture,
      DateTimeStyles.None,
      out beforeDate);
  }

  private static bool TryNormalizeSavedSearchBeforeDate(string value, out string normalized)
  {
    normalized = string.Empty;
    if (string.IsNullOrWhiteSpace(value))
    {
      return false;
    }

    if (!DateOnly.TryParseExact(
      value.Trim(),
      "dd/MM/yyyy",
      CultureInfo.InvariantCulture,
      DateTimeStyles.None,
      out var parsed))
    {
      return false;
    }

    normalized = parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    return true;
  }

  private void UpdateMoveProgress(int movedCount, int? expectedTotal, string batchText, string detailText)
  {
    IsMoveProgressIndeterminate = false;
    if (expectedTotal is null || expectedTotal <= 0)
    {
      MoveProgressPercentage = 0;
      MoveProgressPercentText = string.Empty;
      MoveBatchText = batchText;
      MoveDetailText = detailText;
      MoveProgressText = $"{batchText}. {detailText}";
      OperationMetricsText = string.Empty;
      OperationDownloadedText = string.Empty;
      OperationElapsedText = string.Empty;
      OperationSpeedText = string.Empty;
      OperationEtaText = string.Empty;
      OperationFolderText = string.Empty;
      OperationFileText = string.Empty;
      return;
    }

    var safeTotal = Math.Max(expectedTotal.Value, 1);
    MoveProgressPercentage = Math.Clamp((int)Math.Round(movedCount * 100d / safeTotal), 0, 100);
    MoveProgressPercentText = $"{MoveProgressPercentage}%";
    MoveBatchText = batchText;
    MoveDetailText = detailText;
    MoveProgressText = $"{batchText}. {detailText}";
    OperationMetricsText = string.Empty;
    OperationDownloadedText = string.Empty;
    OperationElapsedText = string.Empty;
    OperationSpeedText = string.Empty;
    OperationEtaText = string.Empty;
    OperationFolderText = string.Empty;
    OperationFileText = string.Empty;
  }

  private async Task AnimateMoveProgressAsync(int fromCount, int toCount, int expectedTotal, string text, CancellationToken cancellationToken)
  {
    var delta = toCount - fromCount;
    if (delta <= 0)
    {
      UpdateMoveProgress(toCount, expectedTotal, text, $"{toCount}/{expectedTotal} messaggi spostati.");
      return;
    }

    var delay = TimeSpan.FromMilliseconds(Math.Clamp(500 / delta, 8, 35));
    for (var movedCount = fromCount + 1; movedCount <= toCount; movedCount++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      UpdateMoveProgress(movedCount, expectedTotal, text, $"{movedCount}/{expectedTotal} messaggi spostati.");
      await Task.Delay(delay, cancellationToken);
    }
  }

  private void BeginOperationMetrics()
  {
    _operationStartedAt = DateTimeOffset.Now;
    _operationSpeedWarmupThreshold = TimeSpan.FromSeconds(Random.Shared.Next(50, 101));
    _operationEtaWarmupThreshold = _operationSpeedWarmupThreshold + TimeSpan.FromSeconds(Random.Shared.Next(15, 61));
    _stableOperationEtaText = "ETA in calcolo";
    _lastOperationEtaUpdate = DateTimeOffset.MinValue;
    _operationMetricsVisible = true;
    _operationCompletedCount = 0;
    _operationTotalCount = null;
    _operationCurrentLabel = string.Empty;
    OperationElapsedText = "Trascorso: 0s";
    OperationSpeedText = "Velocita': in calcolo";
    OperationEtaText = "ETA: in calcolo";
  }

  private void UpdateOperationMetrics(int completedCount, int? totalCount, string currentLabel)
  {
    if (!_operationMetricsVisible)
    {
      return;
    }

    var elapsed = _operationStartedAt == DateTimeOffset.MinValue
      ? TimeSpan.Zero
      : DateTimeOffset.Now - _operationStartedAt;
    if (elapsed < TimeSpan.Zero)
    {
      elapsed = TimeSpan.Zero;
    }

    var speedText = elapsed < _operationSpeedWarmupThreshold || completedCount <= 0
      ? "in calcolo"
      : $"{completedCount / Math.Max(elapsed.TotalSeconds, 1):F1} msg/s";

    _operationCompletedCount = completedCount;
    _operationTotalCount = totalCount;
    _operationCurrentLabel = currentLabel;

    if (totalCount is > 0
      && completedCount > 0
      && elapsed >= _operationEtaWarmupThreshold
      && (_lastOperationEtaUpdate == DateTimeOffset.MinValue || DateTimeOffset.Now - _lastOperationEtaUpdate >= DownloadEtaRefreshInterval))
    {
      var remainingCount = Math.Max(totalCount.Value - completedCount, 0);
      var messagesPerSecond = completedCount / Math.Max(elapsed.TotalSeconds, 1);
      _stableOperationEtaText = $"ETA: {FormatDuration(TimeSpan.FromSeconds(remainingCount / Math.Max(messagesPerSecond, 0.0001)))}";
      _lastOperationEtaUpdate = DateTimeOffset.Now;
    }

    OperationElapsedText = $"Trascorso: {FormatDuration(elapsed)}";
    OperationSpeedText = $"Velocita': {speedText}";
    OperationEtaText = _stableOperationEtaText.StartsWith("ETA:", StringComparison.Ordinal)
      ? _stableOperationEtaText
      : "ETA: in calcolo";
    if (!string.IsNullOrWhiteSpace(currentLabel))
    {
      OperationFolderText = $"Cartella: {currentLabel}";
    }
  }

  private void ClearOperationMetrics()
  {
    _operationMetricsVisible = false;
    _operationStartedAt = DateTimeOffset.MinValue;
    _operationCompletedCount = 0;
    _operationTotalCount = null;
    _operationCurrentLabel = string.Empty;
    _stableOperationEtaText = "ETA in calcolo";
    _lastOperationEtaUpdate = DateTimeOffset.MinValue;
    OperationElapsedText = string.Empty;
    OperationSpeedText = string.Empty;
    OperationEtaText = string.Empty;
  }

  private void StartOperationMetricsTimer()
  {
    StopOperationMetricsTimer();
    _operationMetricsTimer = new DispatcherTimer
    {
      Interval = TimeSpan.FromSeconds(1)
    };
    _operationMetricsTimer.Tick += OperationMetricsTimer_OnTick;
    _operationMetricsTimer.Start();
  }

  private void StopOperationMetricsTimer()
  {
    if (_operationMetricsTimer is null)
    {
      return;
    }

    _operationMetricsTimer.Stop();
    _operationMetricsTimer.Tick -= OperationMetricsTimer_OnTick;
    _operationMetricsTimer = null;
  }

  private void OperationMetricsTimer_OnTick(object? sender, EventArgs e)
  {
    if (!_operationMetricsVisible)
    {
      return;
    }

    UpdateOperationMetrics(_operationCompletedCount, _operationTotalCount, _operationCurrentLabel);
  }

  private async Task<MailMoveResult> MoveMessagesWithRetryAsync(
    CarbonioConnectionSettings settings,
    string password,
    IReadOnlyList<string> messageIds,
    string destinationFolderId,
    int retryCount,
    int retryDelaySeconds,
    CancellationToken cancellationToken)
  {
    var attempts = Math.Max(retryCount, 1);
    var delaySeconds = Math.Max(retryDelaySeconds, 1);

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var result = await _moveDiagnosticService.MoveMessagesAsync(settings, password, messageIds, destinationFolderId, cancellationToken);
      if (result.IsSuccess)
      {
        return result;
      }

      if (attempt >= attempts)
      {
        return result;
      }

      var delay = TimeSpan.FromSeconds(delaySeconds * attempt);
      MoveProgressText = $"Riprovo lo spostamento batch tra {FormatDuration(delay)}...";
      MoveDetailText = $"{MoveProgressText} Errore: {result.Fault?.Reason}";
      StatusMessage = MoveDetailText;
      await Task.Delay(delay, cancellationToken);
    }

    return await _moveDiagnosticService.MoveMessagesAsync(settings, password, messageIds, destinationFolderId, cancellationToken);
  }

  private void ResetMoveProgress()
  {
    StopDownloadMetricsTimer();
    IsMoveInProgress = false;
    _moveCancellationTokenSource = null;
    MoveProgressPercentage = 0;
    IsMoveProgressIndeterminate = false;
    MoveProgressText = "Nessuno spostamento in corso.";
    MoveProgressPercentText = string.Empty;
    MoveBatchText = string.Empty;
    MoveDetailText = MoveProgressText;
    OperationMetricsText = string.Empty;
    OperationDownloadedText = string.Empty;
    OperationElapsedText = string.Empty;
    OperationSpeedText = string.Empty;
    OperationEtaText = string.Empty;
    OperationFolderText = string.Empty;
    OperationFileText = string.Empty;
    _operationMetricsVisible = false;
  }

  private void StartDownloadMetricsTimer()
  {
    StopDownloadMetricsTimer();
    _downloadMetricsTimer = new DispatcherTimer
    {
      Interval = TimeSpan.FromSeconds(1)
    };
    _downloadMetricsTimer.Tick += DownloadMetricsTimer_OnTick;
    _downloadMetricsTimer.Start();
  }

  private void StopDownloadMetricsTimer()
  {
    if (_downloadMetricsTimer is null)
    {
      return;
    }

    _downloadMetricsTimer.Stop();
    _downloadMetricsTimer.Tick -= DownloadMetricsTimer_OnTick;
    _downloadMetricsTimer = null;
    _lastDownloadProgress = null;
    _stableDownloadEtaText = "ETA in calcolo";
    _lastDownloadEtaUpdate = DateTimeOffset.MinValue;
    _downloadStartedAt = DateTimeOffset.MinValue;
  }

  private void DownloadMetricsTimer_OnTick(object? sender, EventArgs e)
  {
    if (_lastDownloadProgress is null)
    {
      return;
    }

    var elapsedProgress = _lastDownloadProgress with { Elapsed = GetMonotonicDownloadElapsed(_lastDownloadProgress.Elapsed) };
    _lastDownloadProgress = elapsedProgress;
    UpdateDownloadMetricTexts(elapsedProgress, elapsedProgress.Elapsed, DateTimeOffset.Now);
  }

  private TimeSpan GetMonotonicDownloadElapsed(TimeSpan serviceElapsed)
  {
    var uiElapsed = _downloadStartedAt == DateTimeOffset.MinValue
      ? serviceElapsed
      : DateTimeOffset.Now - _downloadStartedAt;
    var currentDisplayedElapsed = _lastDownloadProgress?.Elapsed ?? TimeSpan.Zero;
    return new[] { serviceElapsed, uiElapsed, currentDisplayedElapsed, TimeSpan.Zero }.Max();
  }

  private void ResetDownloadWarmupThresholds()
  {
    _downloadSpeedWarmupThreshold = TimeSpan.FromSeconds(Random.Shared.Next(50, 101));
    _downloadEtaWarmupThreshold = _downloadSpeedWarmupThreshold + TimeSpan.FromSeconds(Random.Shared.Next(15, 61));
  }

  private void UpdateDownloadMetricTexts(MailDownloadProgress progress, TimeSpan elapsed, DateTimeOffset now)
  {
    var speedBytesPerSecond = elapsed.TotalSeconds <= 0 ? 0 : progress.BytesDownloaded / elapsed.TotalSeconds;
    var speedText = elapsed < _downloadSpeedWarmupThreshold
      ? "velocita' in calcolo"
      : FormatBytesPerSecond(speedBytesPerSecond);

    if (elapsed >= _downloadEtaWarmupThreshold
      && progress.TotalCount > 0
      && progress.DownloadedThisSessionCount > 0
      && (_lastDownloadEtaUpdate == DateTimeOffset.MinValue || now - _lastDownloadEtaUpdate >= DownloadEtaRefreshInterval))
    {
      var remainingMessages = Math.Max(progress.TotalCount - progress.CompletedCount, 0);
      var secondsPerMessage = elapsed.TotalSeconds / progress.DownloadedThisSessionCount;
      _stableDownloadEtaText = $"ETA: {FormatDuration(TimeSpan.FromSeconds(remainingMessages * secondsPerMessage))}";
      _lastDownloadEtaUpdate = now;
    }

    var skippedText = progress.SkippedCount > 0
      ? $" Gia' presenti saltati: {progress.SkippedCount.ToString(CultureInfo.InvariantCulture)}."
      : string.Empty;
    OperationElapsedText = $"Trascorso: {FormatDuration(elapsed)}";
    OperationSpeedText = $"Velocita': {speedText}";
    OperationDownloadedText = $"MB scaricati: {FormatBytes(progress.BytesDownloaded)}. Completati: {progress.CompletedCount.ToString(CultureInfo.InvariantCulture)}/{Math.Max(progress.TotalCount, 0).ToString(CultureInfo.InvariantCulture)}.{skippedText}";
    OperationEtaText = _stableDownloadEtaText.StartsWith("ETA:", StringComparison.Ordinal)
      ? _stableDownloadEtaText
      : "ETA: in calcolo";
  }

  private static string FormatBytesPerSecond(double bytesPerSecond)
  {
    return $"{FormatBytes((long)Math.Max(bytesPerSecond, 0))}/s";
  }

  private static string FormatBytes(long bytes)
  {
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    var value = (double)Math.Max(bytes, 0);
    var unitIndex = 0;
    while (value >= 1024 && unitIndex < units.Length - 1)
    {
      value /= 1024;
      unitIndex++;
    }

    return unitIndex == 0
      ? $"{value:0} {units[unitIndex]}"
      : $"{value:0.0} {units[unitIndex]}";
  }

  private static string FormatDuration(TimeSpan duration)
  {
    if (duration.TotalHours >= 1)
    {
      return $"{(int)duration.TotalHours:0}h {duration.Minutes:00}m {duration.Seconds:00}s";
    }

    if (duration.TotalMinutes >= 1)
    {
      return $"{duration.Minutes:0}m {duration.Seconds:00}s";
    }

    return $"{Math.Max(duration.Seconds, 0):0}s";
  }

  private FolderSelectionViewModel GetPlannedDestinationFolder(FolderSelectionViewModel sourceFolder)
  {
    if (!UseArchiveDestination)
    {
      return SelectedDestinationFolder!;
    }

    var archivePath = CarbonioArchiveFolderService.BuildArchivePath(sourceFolder.AbsolutePath);
    return new FolderSelectionViewModel(new MailFolder
    {
      Id = "Archive",
      Name = Path.GetFileName(archivePath),
      AbsolutePath = archivePath
    });
  }

  private async Task<ArchiveFolderEnsureResult> ResolveMoveDestinationAsync(
    CarbonioConnectionSettings settings,
    string password,
    FolderSelectionViewModel sourceFolder,
    CancellationToken cancellationToken)
  {
    if (!UseArchiveDestination)
    {
      return new ArchiveFolderEnsureResult(true, ToMailFolder(SelectedDestinationFolder!), "Destinazione selezionata pronta.", []);
    }

    MoveBatchText = "Preparazione Archivio";
    MoveDetailText = "Verifica e creazione cartelle mancanti sotto /Archive...";
    MoveProgressText = MoveDetailText;
    IsMoveProgressIndeterminate = true;
    try
    {
      return await _archiveFolderService.EnsureArchiveDestinationAsync(settings, password, ToMailFolder(sourceFolder), cancellationToken);
    }
    finally
    {
      IsMoveProgressIndeterminate = false;
    }
  }

  private static MailFolder ToMailFolder(FolderSelectionViewModel folder)
  {
    return new MailFolder
    {
      Id = folder.Id,
      Name = folder.Name,
      AbsolutePath = folder.AbsolutePath
    };
  }

  private string? ValidateMovePreview()
  {
    if (PreviewMessages.Count == 0)
    {
      return "Nessun messaggio in preview. Esegui prima Test ricerca.";
    }

    return ValidateMoveSelection();
  }

  private string? ValidateMoveSelection()
  {
    if (SelectedSourceFolder is null)
    {
      return "Seleziona una cartella sorgente.";
    }

    if (UseArchiveDestination)
    {
      if (SelectedSourceFolder.AbsolutePath.StartsWith("/Archive/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(SelectedSourceFolder.AbsolutePath, "/Archive", StringComparison.OrdinalIgnoreCase))
      {
        return "La sorgente e' gia' dentro /Archive.";
      }

      return null;
    }

    if (IncludeSourceSubfolders)
    {
      return "L'opzione sottocartelle richiede la destinazione Archivio.";
    }

    if (SelectedDestinationFolder is null)
    {
      return "Seleziona una cartella destinazione.";
    }

    if (SelectedSourceFolder.Id == SelectedDestinationFolder.Id)
    {
      return "Sorgente e destinazione devono essere diverse.";
    }

    return null;
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
