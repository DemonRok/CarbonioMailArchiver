using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using CarbonioMailArchiver.App.ViewModels;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfListView = System.Windows.Controls.ListView;

namespace CarbonioMailArchiver.App;

public partial class MainWindow : Window
{
  private const int ShowWindowRestore = 9;

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool ShowWindow(IntPtr hWnd, int command);

  private readonly MainWindowViewModel _viewModel;
  private readonly Forms.NotifyIcon _trayIcon;
  private readonly Forms.ContextMenuStrip _trayMenu;
  private string? _logSortMemberPath;
  private ListSortDirection _logSortDirection = ListSortDirection.Descending;

  public MainWindow(MainWindowViewModel viewModel)
  {
    _viewModel = viewModel;
    DataContext = viewModel;
    InitializeComponent();
    _trayMenu = new Forms.ContextMenuStrip();
    _trayMenu.Items.Add("Apri", null, TrayOpen_OnClick);
    _trayMenu.Items.Add(new Forms.ToolStripSeparator());
    _trayMenu.Items.Add("Esci", null, TrayExit_OnClick);
    _trayIcon = new Forms.NotifyIcon
    {
      Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? DrawingSystemIcons.Application,
      Text = "Carbonio Mail Archiver",
      ContextMenuStrip = _trayMenu,
      Visible = _viewModel.MinimizeToTray
    };
    _trayIcon.DoubleClick += TrayIcon_OnDoubleClick;
    _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    Loaded += MainWindow_OnLoaded;
    StateChanged += MainWindow_OnStateChanged;
    Closing += MainWindow_OnClosing;
  }

  private void MainWindow_OnStateChanged(object? sender, EventArgs e)
  {
    if (_viewModel.MinimizeToTray && WindowState == WindowState.Minimized)
    {
      ShowInTaskbar = false;
      Hide();
    }
  }

  private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(MainWindowViewModel.MinimizeToTray))
    {
      _trayIcon.Visible = _viewModel.MinimizeToTray;
      if (!_viewModel.MinimizeToTray && !IsVisible)
      {
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Show();
        Activate();
      }
    }
  }

  private void TrayIcon_OnDoubleClick(object? sender, EventArgs e)
  {
    RestoreFromTray();
  }

  private void TrayOpen_OnClick(object? sender, EventArgs e)
  {
    RestoreFromTray();
  }

  private void TrayExit_OnClick(object? sender, EventArgs e)
  {
    Close();
  }

  private void RestoreFromTray()
  {
    ShowInTaskbar = true;
    WindowState = WindowState.Normal;
    Show();
    var handle = new WindowInteropHelper(this).Handle;
    ShowWindow(handle, ShowWindowRestore);
    Topmost = true;
    Topmost = false;
    SetForegroundWindow(handle);
    Activate();
    Focus();
  }

  private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
  {
    _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
    _trayIcon.Visible = false;
    _trayIcon.Dispose();
    _trayMenu.Dispose();
  }

  private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
  {
    await _viewModel.InitializeAsync();
    if (!string.IsNullOrEmpty(_viewModel.Password))
    {
      PasswordInput.Password = _viewModel.Password;
    }
  }

  private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e)
  {
    _viewModel.Password = PasswordInput.Password;
  }

  private void NumericTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
  {
    e.Handled = !e.Text.All(char.IsDigit);
  }

  private void NumericTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
  {
    if (!e.DataObject.GetDataPresent(WpfDataFormats.Text))
    {
      e.CancelCommand();
      return;
    }

    var text = e.DataObject.GetData(WpfDataFormats.Text) as string;
    if (string.IsNullOrEmpty(text) || !text.All(char.IsDigit))
    {
      e.CancelCommand();
    }
  }

  private void LogColumnHeader_OnClick(object sender, RoutedEventArgs e)
  {
    if (sender is not GridViewColumnHeader header || header.Tag is not string memberPath || string.IsNullOrWhiteSpace(memberPath))
    {
      return;
    }

    var view = _viewModel.RecentLogEntriesView as ListCollectionView;
    if (view is null)
    {
      return;
    }

    if (string.Equals(_logSortMemberPath, memberPath, StringComparison.Ordinal))
    {
      _logSortDirection = _logSortDirection == ListSortDirection.Ascending
        ? ListSortDirection.Descending
        : ListSortDirection.Ascending;
    }
    else
    {
      _logSortMemberPath = memberPath;
      _logSortDirection = ListSortDirection.Ascending;
    }

    view.SortDescriptions.Clear();
    view.SortDescriptions.Add(new SortDescription(memberPath, _logSortDirection));
  }

  private void LogListView_OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    if (sender is not WpfListView listView || listView.SelectedItem is not MainWindowViewModel.LogEntryViewModel entry)
    {
      return;
    }

    WpfClipboard.SetText($"{entry.Timestamp} [{entry.Level}] {entry.Source} {entry.Message}");
  }
}
