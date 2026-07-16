using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Infrastructure.Configuration;
using CarbonioMailArchiver.Infrastructure.Logging;
using CarbonioMailArchiver.Infrastructure.Security;
using CarbonioMailArchiver.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonioMailArchiver.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddCarbonioInfrastructure(this IServiceCollection services)
  {
    services.AddSingleton<AppConfiguration>();
    services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
    services.AddSingleton<IOperationLogService, OperationLogService>();
    services.AddSingleton<IOperationReportService, OperationReportService>();
    services.AddSingleton<IAuthenticationService, PhaseBPendingAuthenticationService>();
    services.AddSingleton<ICarbonioSoapClient, PhaseBPendingCarbonioSoapClient>();
    services.AddSingleton<IConnectionDiagnosticService, CarbonioConnectionDiagnosticService>();
    services.AddSingleton<ISearchDiagnosticService, CarbonioSearchDiagnosticService>();
    services.AddSingleton<IFolderDiagnosticService, CarbonioFolderDiagnosticService>();
    services.AddSingleton<IArchiveFolderService, CarbonioArchiveFolderService>();
    services.AddSingleton<IFolderMaintenanceService, CarbonioFolderMaintenanceService>();
    services.AddSingleton<IMoveDiagnosticService, CarbonioMoveDiagnosticService>();
    services.AddSingleton<IMailSearchService, PhaseBPendingMailSearchService>();
    services.AddSingleton<IMailMoveService, PhaseBPendingMailMoveService>();
    services.AddSingleton<IFolderService, PhaseBPendingFolderService>();
    services.AddSingleton<ICsvExportService, PhaseBPendingCsvExportService>();

    return services;
  }
}
