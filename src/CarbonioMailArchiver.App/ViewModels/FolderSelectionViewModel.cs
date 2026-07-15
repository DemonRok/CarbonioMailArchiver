using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.App.ViewModels;

public sealed class FolderSelectionViewModel
{
  public FolderSelectionViewModel(MailFolder folder)
  {
    Id = folder.Id;
    Name = folder.Name;
    AbsolutePath = folder.AbsolutePath;
    DisplayName = $"{folder.AbsolutePath} ({folder.Id})";
  }

  public string Id { get; }
  public string Name { get; }
  public string AbsolutePath { get; }
  public string DisplayName { get; }
}
