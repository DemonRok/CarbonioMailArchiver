using System.Globalization;
using System.Text;
using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class OperationReportService : IOperationReportService
{
  public OperationReportService()
  {
    ReportDirectory = Path.Combine(AppContext.BaseDirectory, "Reports");
    Directory.CreateDirectory(ReportDirectory);
  }

  public string ReportDirectory { get; }

  public async Task<string> ExportMoveReportAsync(MoveOperationReport report, CancellationToken cancellationToken)
  {
    Directory.CreateDirectory(ReportDirectory);
    var fileName = $"move-report-{report.StartedAt:yyyyMMdd-HHmmss-fff}.csv";
    var path = Path.Combine(ReportDirectory, fileName);

    var builder = new StringBuilder();
    AppendRow(builder, "Campo", "Valore");
    AppendRow(builder, "Account", report.Account);
    AppendRow(builder, "Sorgente", $"{report.SourceFolder} ({report.SourceFolderId})");
    AppendRow(builder, "Destinazione", $"{report.DestinationFolder} ({report.DestinationFolderId})");
    AppendRow(builder, "Cerca prima del", report.BeforeDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
    AppendRow(builder, "Batch", report.BatchSize.ToString(CultureInfo.InvariantCulture));
    AppendRow(builder, "Limite richiesto", report.RequestedLimit == 0 ? "tutte" : report.RequestedLimit.ToString(CultureInfo.InvariantCulture));
    AppendRow(builder, "Inizio", report.StartedAt.ToString("O", CultureInfo.InvariantCulture));
    AppendRow(builder, "Fine", report.FinishedAt.ToString("O", CultureInfo.InvariantCulture));
    AppendRow(builder, "Risultato", report.Result);
    builder.AppendLine();
    AppendRow(builder, "MessageId", "Stato", "Dettaglio");

    foreach (var row in report.Rows)
    {
      cancellationToken.ThrowIfCancellationRequested();
      AppendRow(builder, row.MessageId, row.Status, row.Detail ?? string.Empty);
    }

    await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
    return path;
  }

  private static void AppendRow(StringBuilder builder, params string[] values)
  {
    builder.AppendLine(string.Join(";", values.Select(Escape)));
  }

  private static string Escape(string value)
  {
    var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
    return escaped.Contains(';', StringComparison.Ordinal) ||
      escaped.Contains('"', StringComparison.Ordinal) ||
      escaped.Contains('\n', StringComparison.Ordinal) ||
      escaped.Contains('\r', StringComparison.Ordinal)
        ? $"\"{escaped}\""
        : escaped;
  }
}
