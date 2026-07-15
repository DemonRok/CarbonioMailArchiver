using System.Windows;
using CarbonioMailArchiver.App.ViewModels;
using CarbonioMailArchiver.Infrastructure;
using CarbonioMailArchiver.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.App;

public partial class App : Application
{
  private readonly IHost _host;

  public App()
  {
    _host = Host.CreateDefaultBuilder()
      .ConfigureLogging(builder =>
      {
        builder.ClearProviders();
        builder.AddProvider(new DailyFileLoggerProvider());
        builder.SetMinimumLevel(LogLevel.Information);
      })
      .ConfigureServices(services =>
      {
        services.AddCarbonioInfrastructure();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
      })
      .Build();
  }

  protected override async void OnStartup(StartupEventArgs e)
  {
    await _host.StartAsync();

    var window = _host.Services.GetRequiredService<MainWindow>();
    window.Show();

    base.OnStartup(e);
  }

  protected override async void OnExit(ExitEventArgs e)
  {
    await _host.StopAsync(TimeSpan.FromSeconds(5));
    _host.Dispose();
    base.OnExit(e);
  }
}
