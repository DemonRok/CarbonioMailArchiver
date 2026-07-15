using System.Xml.Linq;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Infrastructure.Services;

internal sealed class PhaseBPendingAuthenticationService : IAuthenticationService
{
  public Task<AuthenticationResult> AuthenticateAsync(CarbonioConnectionSettings settings, string password, CancellationToken cancellationToken)
  {
    return Task.FromResult(new AuthenticationResult(false, null, null, "Autenticazione SOAP reale prevista in Fase B."));
  }

  public Task LogoutAsync(CarbonioSession session, CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}

internal sealed class PhaseBPendingCarbonioSoapClient : ICarbonioSoapClient
{
  public Task<XDocument> SendAsync(CarbonioConnectionSettings settings, XDocument envelope, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Client SOAP Carbonio previsto in Fase B.");
  }
}

internal sealed class PhaseBPendingMailSearchService : IMailSearchService
{
  public Task<MailSearchResult> SearchAsync(CarbonioSession session, MailSearchRequest request, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Ricerca email Carbonio prevista in Fase B.");
  }
}

internal sealed class PhaseBPendingMailMoveService : IMailMoveService
{
  public Task<MailMoveResult> MoveAsync(CarbonioSession session, MailMoveRequest request, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Spostamento email Carbonio previsto in Fase B.");
  }
}

internal sealed class PhaseBPendingFolderService : IFolderService
{
  public Task<IReadOnlyList<MailFolder>> GetFoldersAsync(CarbonioSession session, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Lettura cartelle Carbonio prevista in Fase B.");
  }

  public Task<MailFolder> CreateInboxChildFolderAsync(CarbonioSession session, string folderName, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Creazione cartelle Carbonio prevista in Fase B.");
  }
}

internal sealed class PhaseBPendingCsvExportService : ICsvExportService
{
  public Task ExportPreviewAsync(string path, IEnumerable<MailMessageSummary> messages, CancellationToken cancellationToken)
  {
    throw new NotImplementedException("Export CSV previsto in Fase B.");
  }
}
