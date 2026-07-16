using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CarbonioMailArchiver.App.ViewModels;

namespace CarbonioMailArchiver.App;

public partial class MainWindow : Window
{
  private readonly MainWindowViewModel _viewModel;

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
}
