using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WorklogManager.Services;
using WorklogManager.ViewModels;
using WorklogManager.Views;

namespace WorklogManager;

/// <summary>
/// Application entry point.
/// Configures the DI container and creates the main window.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (_serviceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    // ── Service registration ──────────────────────────────────────────────────

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Settings (singleton — one instance shared across the app) ──
        services.AddSingleton<ISettingsService, SettingsService>();

        // ── HTTP clients (each service gets its own named HttpClient) ──
        services.AddHttpClient<IJiraValidationService, JiraValidationService>();
        services.AddHttpClient<ITempoApiService, TempoApiService>();

        // ── CSV parser (stateless, singleton is fine) ──
        services.AddSingleton<ICsvParserService, CsvParserService>();
        services.AddSingleton<ITimeEntryProvider, TogglTrackCsvTimeEntryProvider>();

        // ── ViewModels (transient — new instance per window) ──
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // ── Windows ──
        // Factory for SettingsWindow so MainViewModel can open it without
        // directly referencing IServiceProvider.
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<Func<SettingsWindow>>(
            sp => () => sp.GetRequiredService<SettingsWindow>());

        services.AddTransient<MainWindow>();
    }
}
