using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Core.Configuration;
using Core.Services;
using Microsoft.Extensions.DependencyInjection;
using UI.Services;
using UI.ViewModels;

namespace UI;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5292/")
        };

        services.AddSingleton(httpClient);
        services.AddSingleton<IAppStateService, PollingAppStateService>();
        services.AddSingleton<IConfigurationService, HttpConfigurationService>();
        services.AddSingleton<ISpotifyAuthService, SpotifyAuthServiceClient>();
    }
}