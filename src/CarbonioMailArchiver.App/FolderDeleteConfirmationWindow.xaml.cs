using System.Windows;

namespace CarbonioMailArchiver.App;

public partial class FolderDeleteConfirmationWindow : Window
{
  public FolderDeleteConfirmationWindow(string summary, string detail)
  {
    InitializeComponent();
    SummaryText.Text = summary;
    DetailText.Text = detail;
  }

  private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
  {
    DialogResult = true;
    Close();
  }

  private void CancelButton_OnClick(object sender, RoutedEventArgs e)
  {
    DialogResult = false;
    Close();
  }
}
