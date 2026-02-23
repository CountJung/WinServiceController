using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using WinServiceController.Services;
using WinServiceController.ViewModels.Pages;
using WinServiceController.ViewModels.Windows;
using WinServiceController.Views.Pages;
using WinServiceController.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace WinServiceController
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)!); })
            .UseSerilog((context, config) =>
            {
                config
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        Path.Combine(AppContext.BaseDirectory, "logs", "app-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        fileSizeLimitBytes: 5 * 1024 * 1024);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Navigation
                services.AddSingleton<INavigationService, NavigationService>();

                // IPC Client
                services.AddSingleton<IPipeClientService, PipeClientService>();

                // Main window
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                // Pages
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ServiceListPage>();
                services.AddSingleton<ServiceListViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services => _host.Services;

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            Log.Information("Application starting");
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            Log.Information("Application shutting down");
            await _host.StopAsync();
            Log.CloseAndFlush();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled exception");
        }
    }
}
