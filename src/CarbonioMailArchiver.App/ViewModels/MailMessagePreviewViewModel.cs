using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.App.ViewModels;

public sealed class MailMessagePreviewViewModel
{
  private static readonly IReadOnlyDictionary<string, string> KnownFolderPaths = new Dictionary<string, string>
  {
    ["1"] = "/USER_ROOT",
    ["2"] = "/Inbox",
    ["3"] = "/Trash",
    ["4"] = "/Junk",
    ["5"] = "/Sent",
    ["6"] = "/Drafts"
  };

  public MailMessagePreviewViewModel(MailMessageSummary message, IReadOnlyDictionary<string, MailFolder> foldersById)
  {
    Id = message.Id;
    Date = message.Date;
    From = message.From;
    Subject = message.Subject;
    Size = message.Size;
    FolderId = message.FolderId;
    FolderPath = ResolveFolderPath(message.FolderId, foldersById);
  }

  public MailMessagePreviewViewModel(string id, string subject, string folderPath)
  {
    Id = id;
    Date = null;
    From = string.Empty;
    Subject = subject;
    Size = null;
    FolderId = id;
    FolderPath = folderPath;
  }

  public string Id { get; }
  public DateTimeOffset? Date { get; }
  public string From { get; }
  public string Subject { get; }
  public long? Size { get; }
  public string FolderId { get; }
  public string FolderPath { get; }

  private static string ResolveFolderPath(string folderId, IReadOnlyDictionary<string, MailFolder> foldersById)
  {
    if (foldersById.TryGetValue(folderId, out var folder) && !string.IsNullOrWhiteSpace(folder.AbsolutePath))
    {
      return folder.AbsolutePath;
    }

    if (KnownFolderPaths.TryGetValue(folderId, out var knownPath))
    {
      return knownPath;
    }

    return folderId;
  }
}
