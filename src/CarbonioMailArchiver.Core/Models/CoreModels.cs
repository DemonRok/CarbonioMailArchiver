namespace CarbonioMailArchiver.Core.Models;

public sealed record AuthenticationResult(bool IsSuccess, CarbonioSession? Session, SoapFaultInfo? Fault, string Message);

public sealed record CarbonioSession(string Account, Uri SoapEndpoint, string AuthToken, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

public sealed record ConnectionDiagnosticResult(bool IsSuccess, string Message, string? AccountName, string? ServerVersion);

public sealed record SearchDiagnosticResult(bool IsSuccess, string Message, IReadOnlyList<MailMessageSummary> Messages, int? TotalCount, bool HasMore);

public sealed class MailFolder
{
  public string Id { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string AbsolutePath { get; set; } = string.Empty;
  public string? ParentId { get; set; }
  public bool IsInbox { get; set; }
  public bool IsWritable { get; set; } = true;
  public List<MailFolder> Children { get; } = [];
}

public sealed record MailMessageSummary(string Id, DateTimeOffset? Date, string From, string Subject, long? Size, string FolderId);

public sealed record MailSearchRequest(DateOnly BeforeDate, int Limit, string SourceFolderQuery = "in:inbox");

public sealed record MailSearchResult(IReadOnlyList<MailMessageSummary> Messages, int? TotalCount, bool HasMore);

public sealed record MailMoveRequest(IReadOnlyList<string> MessageIds, string DestinationFolderId);

public sealed record MailMoveResult(int RequestedCount, int MovedCount, IReadOnlyList<string> FailedMessageIds, SoapFaultInfo? Fault);

public enum ArchiveOperationMode
{
  Analysis,
  Simulation,
  Move
}

public sealed class ArchiveOperationOptions
{
  public ArchiveOperationMode Mode { get; set; } = ArchiveOperationMode.Analysis;
  public DateOnly BeforeDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
  public int BatchSize { get; set; } = 200;
  public int MaxFailedBatchRepeats { get; set; } = 3;
  public string DestinationFolderId { get; set; } = string.Empty;
}

public sealed record ArchiveOperationProgress(int InitialCount, int ProcessedCount, int RemainingCount, int ErrorCount, TimeSpan Elapsed);

public sealed record ArchiveOperationResult(bool IsSuccess, int InitialCount, int MovedCount, int ErrorCount, string Message);

public sealed record SoapFaultInfo(string Code, string Reason, string? Detail);
