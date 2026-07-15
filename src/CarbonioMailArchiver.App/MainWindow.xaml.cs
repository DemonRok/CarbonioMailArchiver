using System.Windows;
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
}
