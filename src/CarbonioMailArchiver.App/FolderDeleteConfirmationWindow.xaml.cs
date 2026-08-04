using System.Windows;

namespace CarbonioMailArchiver.App;

public partial class FolderDeleteConfirmationWindow : Window
{
  public bool? ConfirmationResult { get; private set; }

  public FolderDeleteConfirmationWindow(string summary, string detail)
  {
    InitializeComponent();
    SummaryText.Text = summary;
    DetailText.Text = detail;
  }

  private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
  {
    ConfirmationResult = true;
    Close();
  }

  private void CancelButton_OnClick(object sender, RoutedEventArgs e)
  {
    ConfirmationResult = false;
    Close();
  }
}
