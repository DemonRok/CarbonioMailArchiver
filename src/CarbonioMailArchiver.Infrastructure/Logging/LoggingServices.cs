using CarbonioMailArchiver.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Logging;

public sealed class DailyFileLoggerProvider : ILoggerProvider
{
  private readonly string _directory;
  private readonly object _sync = new();

  public DailyFileLoggerProvider()
  {
    _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CarbonioMailArchiver", "Logs");
    Directory.CreateDirectory(_directory);
  }

  public ILogger CreateLogger(string categoryName)
  {
    return new DailyFileLogger(_directory, categoryName, _sync);
  }

  public void Dispose()
  {
  }
}

internal sealed class DailyFileLogger(string directory, string categoryName, object sync) : ILogger
{
  public IDisposable? BeginScope<TState>(TState state)
    where TState : notnull
  {
    return null;
  }

  public bool IsEnabled(LogLevel logLevel)
  {
    return logLevel != LogLevel.None;
  }

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
  {
    if (!IsEnabled(logLevel))
    {
      return;
    }

    var filePath = Path.Combine(directory, $"carbonio-mail-archiver-{DateTime.Today:yyyyMMdd}.log");
    var line = $"{DateTimeOffset.Now:O}\t{logLevel}\t{categoryName}\t{formatter(state, exception)}";
    if (exception is not null)
    {
      line += $"\t{exception.GetType().Name}: {exception.Message}";
    }

    lock (sync)
    {
      File.AppendAllText(filePath, line + Environment.NewLine);
    }
  }
}

public sealed class OperationLogService : IOperationLogService
{
  public OperationLogService()
  {
    LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CarbonioMailArchiver", "Logs");
    Directory.CreateDirectory(LogDirectory);
  }

  public string LogDirectory { get; }

  public async Task<IReadOnlyList<string>> ReadRecentLinesAsync(int maxLines, CancellationToken cancellationToken)
  {
    var file = Directory
      .EnumerateFiles(LogDirectory, "carbonio-mail-archiver-*.log")
      .OrderByDescending(File.GetLastWriteTimeUtc)
      .FirstOrDefault();

    if (file is null)
    {
      return [];
    }

    var lines = await File.ReadAllLinesAsync(file, cancellationToken);
    return lines.TakeLast(maxLines).ToArray();
  }
}
