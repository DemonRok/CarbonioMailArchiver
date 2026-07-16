using System.Xml.Linq;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Core.Abstractions;

public interface IAuthenticationService
{
  Task<AuthenticationResult> AuthenticateAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken);
  Task LogoutAsync(CarbonioSession session, CancellationToken cancellationToken);
}

public interface ICarbonioSoapClient
{
  Task<XDocument> SendAsync(CarbonioConnectionSettings settings, XDocument envelope, CancellationToken cancellationToken);
}

public interface IConnectionDiagnosticService
{
  Task<ConnectionDiagnosticResult> TestConnectionAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken);
}

public interface ISearchDiagnosticService
{
  Task<SearchDiagnosticResult> SearchInboxBeforeAsync(CarbonioConnectionSettings settings, string password, MailSearchRequest request, CancellationToken cancellationToken);
}

public interface IFolderDiagnosticService
{
  Task<IReadOnlyDictionary<string, MailFolder>> GetFoldersByIdAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken);
}

public interface IMoveDiagnosticService
{
  Task<MailMoveResult> MoveMessagesAsync(CarbonioConnectionSettings settings, string password, IReadOnlyList<string> messageIds, string destinationFolderId, CancellationToken cancellationToken);
}

public interface IMailSearchService
{
  Task<MailSearchResult> SearchAsync(CarbonioSession session, MailSearchRequest request, CancellationToken cancellationToken);
}

public interface IMailMoveService
{
  Task<MailMoveResult> MoveAsync(CarbonioSession session, MailMoveRequest request, CancellationToken cancellationToken);
}

public interface IFolderService
{
  Task<IReadOnlyList<MailFolder>> GetFoldersAsync(CarbonioSession session, CancellationToken cancellationToken);
  Task<MailFolder> CreateInboxChildFolderAsync(CarbonioSession session, string folderName, CancellationToken cancellationToken);
}

public interface ICredentialStore
{
  Task SavePasswordAsync(string account, string password, CancellationToken cancellationToken);
  Task<string?> ReadPasswordAsync(string account, CancellationToken cancellationToken);
  Task DeletePasswordAsync(string account, CancellationToken cancellationToken);
}

public interface IOperationLogService
{
  Task<IReadOnlyList<string>> ReadRecentLinesAsync(int maxLines, CancellationToken cancellationToken);
  Task ClearAsync(CancellationToken cancellationToken);
  string LogDirectory { get; }
}

public interface IOperationReportService
{
  Task<string> ExportMoveReportAsync(MoveOperationReport report, CancellationToken cancellationToken);
  string ReportDirectory { get; }
}

public interface ICsvExportService
{
  Task ExportPreviewAsync(string path, IEnumerable<MailMessageSummary> messages, CancellationToken cancellationToken);
}
