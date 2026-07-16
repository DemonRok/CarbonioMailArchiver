using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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
  private readonly IFolderDiagnosticService _folderDiagnosticService;
  private readonly IMoveDiagnosticService _moveDiagnosticService;
  private readonly IOperationReportService _operationReportService;
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
  private bool _promptReportExportAfterMove = true;
  private bool _isMoveInProgress;
  private int _timeoutSeconds = 100;
  private int _previewMessageLimit = 10;
  private int _batchSize = 50;
  private int _maxMessagesToMove;
  private int _moveProgressPercentage;
  private bool _isMoveProgressIndeterminate;
  private string _moveProgressText = "Nessuno spostamento in corso.";
  private string _moveProgressPercentText = string.Empty;
  private string _moveBatchText = string.Empty;
  private string _moveDetailText = "Nessuno spostamento in corso.";
  private CancellationTokenSource? _moveCancellationTokenSource;
  private readonly AsyncRelayCommand _moveAllSearchResultsCommand;
  private readonly AsyncRelayCommand _cancelMoveCommand;
  private const string RepositoryUrl = "https://github.com/DemonRok/CarbonioMailArchiver";
  private const string ReleasesUrl = "https://github.com/DemonRok/CarbonioMailArchiver/releases";
  private const string IssuesUrl = "https://github.com/DemonRok/CarbonioMailArchiver/issues";
  private static readonly string CurrentVersion =
    typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
    ?? "dev";

  public MainWindowViewModel(
    AppConfiguration configuration,
    ICredentialStore credentialStore,
    IOperationLogService operationLogService,
    IConnectionDiagnosticService connectionDiagnosticService,
    ISearchDiagnosticService searchDiagnosticService,
    IFolderDiagnosticService folderDiagnosticService,
    IMoveDiagnosticService moveDiagnosticService,
    IOperationReportService operationReportService,
    ILogger<MainWindowViewModel> logger)
  {
    _configuration = configuration;
    _credentialStore = credentialStore;
    _operationLogService = operationLogService;
    _connectionDiagnosticService = connectionDiagnosticService;
    _searchDiagnosticService = searchDiagnosticService;
    _folderDiagnosticService = folderDiagnosticService;
    _moveDiagnosticService = moveDiagnosticService;
    _operationReportService = operationReportService;
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
    RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
    CopyLogsCommand = new AsyncRelayCommand(CopyLogsAsync);
    ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);
    LogDirectory = operationLogService.LogDirectory;
    OpenAppDataCommand = new AsyncRelayCommand(() => OpenPathAsync(ApplicationDirectory));
    OpenLogsCommand = new AsyncRelayCommand(() => OpenPathAsync(LogDirectory));
    OpenReportsCommand = new AsyncRelayCommand(() => OpenPathAsync(ReportDirectory));
    OpenLastReportCommand = new AsyncRelayCommand(OpenLastReportAsync);
    OpenRepositoryCommand = new AsyncRelayCommand(() => OpenPathAsync(RepositoryUrl));
    OpenReleasesCommand = new AsyncRelayCommand(() => OpenPathAsync(ReleasesUrl));
    OpenLicenseCommand = new AsyncRelayCommand(OpenLicenseAsync);
    ReportIssueCommand = new AsyncRelayCommand(() => OpenPathAsync(IssuesUrl));
    RestoreConfigurationDefaultsCommand = new AsyncRelayCommand(RestoreConfigurationDefaultsAsync);
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
  public ICommand RefreshLogsCommand { get; }
  public ICommand CopyLogsCommand { get; }
  public ICommand ClearLogsCommand { get; }
  public ICommand OpenAppDataCommand { get; }
  public ICommand OpenLogsCommand { get; }
  public ICommand OpenReportsCommand { get; }
  public ICommand OpenLastReportCommand { get; }
  public ICommand OpenRepositoryCommand { get; }
  public ICommand OpenReleasesCommand { get; }
  public ICommand OpenLicenseCommand { get; }
  public ICommand ReportIssueCommand { get; }
  public ICommand RestoreConfigurationDefaultsCommand { get; }
  public ICommand DecreasePreviewMessageLimitCommand => new AsyncRelayCommand(() => UpdatePreviewMessageLimitAsync(-1));
  public ICommand IncreasePreviewMessageLimitCommand => new AsyncRelayCommand(() => UpdatePreviewMessageLimitAsync(1));
  public ICommand DecreaseBatchSizeCommand => new AsyncRelayCommand(() => UpdateBatchSizeAsync(-1));
  public ICommand IncreaseBatchSizeCommand => new AsyncRelayCommand(() => UpdateBatchSizeAsync(1));
  public ICommand DecreaseMaxMessagesToMoveCommand => new AsyncRelayCommand(() => UpdateMaxMessagesToMoveAsync(-1));
  public ICommand IncreaseMaxMessagesToMoveCommand => new AsyncRelayCommand(() => UpdateMaxMessagesToMoveAsync(1));
  public ObservableCollection<string> RecentLogLines { get; } = [];
  public ObservableCollection<MailMessagePreviewViewModel> PreviewMessages { get; } = [];
  public ObservableCollection<FolderSelectionViewModel> AvailableFolders { get; } = [];
  public string LogDirectory { get; }
  public string ReportDirectory => _operationReportService.ReportDirectory;
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

  public string SearchBeforeDate
  {
    get => _searchBeforeDate;
    set => SetField(ref _searchBeforeDate, value);
  }

  public FolderSelectionViewModel? SelectedSourceFolder
  {
    get => _selectedSourceFolder;
    set => SetField(ref _selectedSourceFolder, value);
  }

  public FolderSelectionViewModel? SelectedDestinationFolder
  {
    get => _selectedDestinationFolder;
    set => SetField(ref _selectedDestinationFolder, value);
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
    set => SetField(ref _batchSize, Math.Clamp(value, 10, 100));
  }

  public int MaxMessagesToMove
  {
    get => _maxMessagesToMove;
    set => SetField(ref _maxMessagesToMove, Math.Max(value, 0));
  }

  public bool IsMoveInProgress
  {
    get => _isMoveInProgress;
    private set
    {
      SetField(ref _isMoveInProgress, value);
      _moveAllSearchResultsCommand.RaiseCanExecuteChanged();
      _cancelMoveCommand.RaiseCanExecuteChanged();
    }
  }

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
    PromptReportExportAfterMove = settings.PromptReportExportAfterMove;
    TimeoutSeconds = settings.TimeoutSeconds;
    PreviewMessageLimit = Math.Clamp(settings.PreviewMessageLimit, 1, 100);
    BatchSize = Math.Clamp(settings.BatchSize, 10, 100);
    MaxMessagesToMove = Math.Max(settings.MaxMessagesToMove, 0);
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
    var foldersById = await _folderDiagnosticService.GetFoldersByIdAsync(settings, password, CancellationToken.None);
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
    var destinationFolder = SelectedDestinationFolder!;
    await SaveSettingsSnapshotAsync();
    _logger.LogInformation(
      "Simulazione spostamento: {Count} messaggi da {SourceFolder} ({SourceId}) a {DestinationFolder} ({DestinationId}).",
      PreviewMessages.Count,
      sourceFolder.AbsolutePath,
      sourceFolder.Id,
      destinationFolder.AbsolutePath,
      destinationFolder.Id);

    StatusMessage = $"Simulazione: {PreviewMessages.Count} messaggi verrebbero spostati da {sourceFolder.AbsolutePath} a {destinationFolder.AbsolutePath}. Nessuna modifica eseguita.";
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
    var destinationFolder = SelectedDestinationFolder!;
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
    var destinationFolder = SelectedDestinationFolder!;
    await SaveSettingsSnapshotAsync();
    var batchSize = Math.Clamp(BatchSize, 10, 100);
    var maxMessagesToMove = Math.Max(MaxMessagesToMove, 0);
    var password = await GetPasswordAsync(settings);
    var sourceFolderQuery = $"inid:{sourceFolder.Id}";
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
    (bool IsSuccess, string Message, IReadOnlyList<string> MessageIds) scanResult;
    try
    {
      scanResult = await ScanMessageIdsAsync(settings, password, beforeDate, sourceFolderQuery, batchSize, maxMessagesToMove, moveCancellation.Token);
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Conteggio annullato dall'utente.";
      MoveProgressText = "Conteggio annullato.";
      MoveBatchText = "Conteggio annullato";
      MoveDetailText = MoveProgressText;
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    if (!scanResult.IsSuccess)
    {
      StatusMessage = scanResult.Message;
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    if (scanResult.MessageIds.Count == 0)
    {
      StatusMessage = "Nessun messaggio trovato da spostare.";
      PreviewMessages.Clear();
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    var expectedTotal = scanResult.MessageIds.Count;
    MoveProgressPercentage = 0;
    IsMoveProgressIndeterminate = false;
    MoveProgressPercentText = "0%";
    MoveBatchText = "Pronto";
    MoveProgressText = $"Pronto a spostare {expectedTotal} messaggi.";
    MoveDetailText = MoveProgressText;
    var totalDescription = expectedTotal.ToString(CultureInfo.InvariantCulture);
    var limitDescription = maxMessagesToMove == 0
      ? "tutti i messaggi trovati"
      : $"massimo {maxMessagesToMove.ToString(CultureInfo.InvariantCulture)} messaggi";
    var confirmation = MessageBox.Show(
      $"Spostare realmente {totalDescription} messaggi da {sourceFolder.AbsolutePath} a {destinationFolder.AbsolutePath}?\n\nData limite: prima del {beforeDate:dd/MM/yyyy}\nLimite richiesto: {limitDescription}\nBatch: {batchSize} messaggi per volta",
      "Conferma spostamento batch",
      MessageBoxButton.YesNo,
      MessageBoxImage.Warning,
      MessageBoxResult.No);
    if (confirmation != MessageBoxResult.Yes)
    {
      StatusMessage = "Spostamento batch annullato.";
      await RefreshLogsAsync();
      ResetMoveProgress();
      return;
    }

    var operationStartedAt = DateTimeOffset.Now;
    var reportRows = scanResult.MessageIds
      .Select(id => new MoveOperationReportRow(id, "Da spostare", null))
      .ToList();

    try
    {
      var movedCount = 0;
      var batchNumber = 0;
      var reportOffset = 0;

      foreach (var messageIdBatch in scanResult.MessageIds.Chunk(batchSize))
      {
        moveCancellation.Token.ThrowIfCancellationRequested();
        batchNumber++;
        UpdateMoveProgress(movedCount, expectedTotal, $"Batch {batchNumber} in corso", $"Spostati finora: {movedCount}/{expectedTotal} messaggi.");
        StatusMessage = $"{MoveBatchText}. {MoveDetailText}";
        var moveResult = await _moveDiagnosticService.MoveMessagesAsync(settings, password, messageIdBatch, destinationFolder.Id, moveCancellation.Token);
        if (!moveResult.IsSuccess)
        {
          foreach (var failedId in messageIdBatch)
          {
            reportRows[reportOffset++] = new MoveOperationReportRow(failedId, "Errore", moveResult.Fault?.Reason);
          }

          var errorReportPath = await AskAndSaveMoveReportAsync(
            operationStartedAt,
            settings.Email,
            sourceFolder,
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
          return;
        }

        foreach (var movedId in messageIdBatch)
        {
          reportRows[reportOffset++] = new MoveOperationReportRow(movedId, "Spostato", null);
        }

        var previousMovedCount = movedCount;
        movedCount += moveResult.MovedCount;
        await AnimateMoveProgressAsync(previousMovedCount, movedCount, expectedTotal, $"Batch {batchNumber} completato", moveCancellation.Token);
        _logger.LogInformation(
          "Spostamento batch {BatchNumber} completato. Messaggi spostati nel batch: {BatchMoved}. Totale spostato: {MovedCount}.",
          batchNumber,
          moveResult.MovedCount,
          movedCount);
      }

      PreviewMessages.Clear();
      UpdateMoveProgress(movedCount, movedCount, "Spostamento completato", $"{movedCount} messaggi spostati.");
      var successReportPath = await AskAndSaveMoveReportAsync(
        operationStartedAt,
        settings.Email,
        sourceFolder,
        destinationFolder,
        beforeDate,
        batchSize,
        maxMessagesToMove,
        reportRows,
        "Completato",
        CancellationToken.None);
      StatusMessage = $"Spostamento batch completato. Messaggi spostati: {movedCount}.{FormatReportStatus(successReportPath)}";
      await RefreshLogsAsync();
    }
    catch (OperationCanceledException)
    {
      var reportPath = await AskAndSaveMoveReportAsync(
        operationStartedAt,
        settings.Email,
        sourceFolder,
        destinationFolder,
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
      await RefreshLogsAsync();
    }
    finally
    {
      IsMoveInProgress = false;
      _moveCancellationTokenSource = null;
    }
  }

  private Task CancelMoveAsync()
  {
    _moveCancellationTokenSource?.Cancel();
    StatusMessage = "Annullamento richiesto. Attendo il completamento del batch corrente...";
    MoveProgressText = "Annullamento richiesto...";
    MoveBatchText = "Annullamento richiesto";
    MoveDetailText = "Attendo il completamento del batch corrente...";
    IsMoveProgressIndeterminate = false;
    return Task.CompletedTask;
  }

  private async Task<(bool IsSuccess, string Message, IReadOnlyList<string> MessageIds)> ScanMessageIdsAsync(
    CarbonioConnectionSettings settings,
    string password,
    DateOnly beforeDate,
    string sourceFolderQuery,
    int batchSize,
    int maxMessagesToMove,
    CancellationToken cancellationToken)
  {
    var messageIds = new List<string>();
    var knownIds = new HashSet<string>(StringComparer.Ordinal);
    var offset = 0;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      MoveProgressText = maxMessagesToMove == 0
        ? $"Conteggio effettivo in corso: {messageIds.Count} messaggi trovati..."
        : $"Conteggio effettivo in corso: {messageIds.Count}/{maxMessagesToMove} messaggi selezionati...";
      MoveBatchText = "Conteggio";
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

  private async Task ClearLogsAsync()
  {
    await _operationLogService.ClearAsync(CancellationToken.None);
    RecentLogLines.Clear();
    RecentLogText = string.Empty;
    StatusMessage = "Log cancellato.";
  }

  private Task UpdatePreviewMessageLimitAsync(int delta)
  {
    PreviewMessageLimit = Math.Clamp(PreviewMessageLimit + delta, 1, 100);
    return Task.CompletedTask;
  }

  private Task UpdateBatchSizeAsync(int delta)
  {
    BatchSize = Math.Clamp(BatchSize + delta, 10, 100);
    return Task.CompletedTask;
  }

  private Task UpdateMaxMessagesToMoveAsync(int delta)
  {
    MaxMessagesToMove = Math.Max(MaxMessagesToMove + delta, 0);
    return Task.CompletedTask;
  }

  private Task RestoreConfigurationDefaultsAsync()
  {
    TimeoutSeconds = 100;
    PreviewMessageLimit = 10;
    BatchSize = 50;
    MaxMessagesToMove = 0;
    AutoLoadFoldersOnStartup = false;
    DiagnosticSoapLoggingEnabled = false;
    PromptReportExportAfterMove = true;
    StatusMessage = "Default configurazione ripristinati. Premi Salva configurazione per renderli permanenti.";
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

  private CarbonioConnectionSettings ToSettings()
  {
    return new CarbonioConnectionSettings
    {
      BaseUrl = BaseUrl.Trim(),
      SoapUrl = SoapUrl.Trim(),
      Email = Email.Trim(),
      LastSourceFolderId = SelectedSourceFolder?.Id ?? _lastSourceFolderId,
      LastDestinationFolderId = SelectedDestinationFolder?.Id ?? _lastDestinationFolderId,
      RememberCredentials = RememberCredentials,
      AcceptUntrustedCertificates = false,
      DiagnosticSoapLoggingEnabled = DiagnosticSoapLoggingEnabled,
      AutoLoadFoldersOnStartup = AutoLoadFoldersOnStartup,
      TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600),
      PreviewMessageLimit = Math.Clamp(PreviewMessageLimit, 1, 100),
      BatchSize = Math.Clamp(BatchSize, 10, 100),
      MaxMessagesToMove = Math.Max(MaxMessagesToMove, 0),
      PromptReportExportAfterMove = PromptReportExportAfterMove,
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
      return;
    }

    var safeTotal = Math.Max(expectedTotal.Value, 1);
    MoveProgressPercentage = Math.Clamp((int)Math.Round(movedCount * 100d / safeTotal), 0, 100);
    MoveProgressPercentText = $"{MoveProgressPercentage}%";
    MoveBatchText = batchText;
    MoveDetailText = detailText;
    MoveProgressText = $"{batchText}. {detailText}";
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

  private void ResetMoveProgress()
  {
    IsMoveInProgress = false;
    _moveCancellationTokenSource = null;
    MoveProgressPercentage = 0;
    IsMoveProgressIndeterminate = false;
    MoveProgressText = "Nessuno spostamento in corso.";
    MoveProgressPercentText = string.Empty;
    MoveBatchText = string.Empty;
    MoveDetailText = MoveProgressText;
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
