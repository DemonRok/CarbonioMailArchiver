using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.App.ViewModels;

public sealed class MailMessagePreviewViewModel
{
  public MailMessagePreviewViewModel(MailMessageSummary message, IReadOnlyDictionary<string, MailFolder> foldersById)
  {
    Id = message.Id;
    Date = message.Date;
    From = message.From;
    Subject = message.Subject;
    Size = message.Size;
    FolderId = message.FolderId;
    FolderPath = foldersById.TryGetValue(message.FolderId, out var folder) && !string.IsNullOrWhiteSpace(folder.AbsolutePath)
      ? folder.AbsolutePath
      : message.FolderId;
  }

  public string Id { get; }
  public DateTimeOffset? Date { get; }
  public string From { get; }
  public string Subject { get; }
  public long? Size { get; }
  public string FolderId { get; }
  public string FolderPath { get; }
}
