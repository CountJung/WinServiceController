using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;

namespace WinServiceController.Services
{
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        private INavigationWindow? _navigationWindow;

        public ApplicationHostService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleActivationAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private Task HandleActivationAsync()
        {
            if (!Application.Current.Windows.OfType<Views.Windows.MainWindow>().Any())
            {
                // Apply saved theme before showing the window
                var settings = _serviceProvider.GetRequiredService<IUserSettingsService>();
                switch (settings.Settings.Theme)
                {
                    case "Dark":
                        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                        break;
                    case "Light":
                        ApplicationThemeManager.Apply(ApplicationTheme.Light);
                        break;
                    default:
                        var sys = ApplicationThemeManager.GetSystemTheme();
                        var theme = sys is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow
                            ? ApplicationTheme.Dark
                            : ApplicationTheme.Light;
                        ApplicationThemeManager.Apply(theme);
                        break;
                }

                _navigationWindow = (_serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow)!;
                _navigationWindow.ShowWindow();
                _navigationWindow.Navigate(typeof(Views.Pages.DashboardPage));
            }

            return Task.CompletedTask;
        }
    }
}
