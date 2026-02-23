using System.ServiceProcess;
using System.Windows.Threading;
using WinServiceController.Models;
using WinServiceController.Services;
using Wpf.Ui.Abstractions.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private readonly IPipeClientService _pipeClient;
        private DispatcherTimer? _refreshTimer;
        private bool _isInitialized;

        [ObservableProperty]
        private int _totalServices;

        [ObservableProperty]
        private int _runningServices;

        [ObservableProperty]
        private int _stoppedServices;

        [ObservableProperty]
        private bool _isEngineConnected;

        [ObservableProperty]
        private string _engineStatus = "Disconnected";

        public DashboardViewModel(IPipeClientService pipeClient)
        {
            _pipeClient = pipeClient;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _refreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _refreshTimer.Tick += (_, _) => _ = RefreshAllAsync();
            }

            _refreshTimer?.Start();
            _ = RefreshAllAsync();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            _refreshTimer?.Stop();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            RefreshServiceCounts();
            await CheckEngineConnectionAsync();
        }

        private void RefreshServiceCounts()
        {
            try
            {
                var services = ServiceController.GetServices();
                TotalServices = services.Length;
                RunningServices = services.Count(s => s.Status == ServiceControllerStatus.Running);
                StoppedServices = services.Count(s => s.Status == ServiceControllerStatus.Stopped);
            }
            catch
            {
                TotalServices = 0;
                RunningServices = 0;
                StoppedServices = 0;
            }
        }

        private async Task CheckEngineConnectionAsync()
        {
            try
            {
                var response = await _pipeClient.SendCommandAsync(new IpcRequest
                {
                    Command = "PING"
                });

                IsEngineConnected = response is { Status: "PONG" };
                EngineStatus = IsEngineConnected ? "Connected" : "Disconnected";
            }
            catch
            {
                IsEngineConnected = false;
                EngineStatus = "Disconnected";
            }
        }
    }
}
