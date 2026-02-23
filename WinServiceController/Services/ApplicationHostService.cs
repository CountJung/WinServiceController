using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;

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
                _navigationWindow = (_serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow)!;
                _navigationWindow.ShowWindow();
                _navigationWindow.Navigate(typeof(Views.Pages.DashboardPage));
            }

            return Task.CompletedTask;
        }
    }
}
