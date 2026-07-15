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
  }

  private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e)
  {
    _viewModel.Password = PasswordInput.Password;
  }
}
