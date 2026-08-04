using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CarbonioMailArchiver.App.ViewModels;

namespace CarbonioMailArchiver.App;

public partial class MainWindow : Window
{
  private readonly MainWindowViewModel _viewModel;
  private string? _logSortMemberPath;
  private ListSortDirection _logSortDirection = ListSortDirection.Descending;

  public MainWindow(MainWindowViewModel viewModel)
  {
    _viewModel = viewModel;
    DataContext = viewModel;
    InitializeComponent();
    Loaded += MainWindow_OnLoaded;
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
    if (!e.DataObject.GetDataPresent(DataFormats.Text))
    {
      e.CancelCommand();
      return;
    }

    var text = e.DataObject.GetData(DataFormats.Text) as string;
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
    if (sender is not ListView listView || listView.SelectedItem is not MainWindowViewModel.LogEntryViewModel entry)
    {
      return;
    }

    Clipboard.SetText($"{entry.Timestamp} [{entry.Level}] {entry.Source} {entry.Message}");
  }
}
