using System.Collections.ObjectModel;
using System.ServiceProcess;
using WinServiceController.Models;
using WinServiceController.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class ServiceListViewModel : ObservableObject, INavigationAware
    {
        private readonly IPipeClientService _pipeClient;
        private readonly ISnackbarService _snackbar;
        private bool _isInitialized;

        [ObservableProperty]
        private ObservableCollection<ServiceInfo> _services = [];

        [ObservableProperty]
        private ServiceInfo? _selectedService;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ServiceListViewModel(IPipeClientService pipeClient, ISnackbarService snackbarService)
        {
            _pipeClient = pipeClient;
            _snackbar = snackbarService;
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
            }

            await RefreshServicesAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            try
            {
                var controllers = ServiceController.GetServices();
                var filtered = string.IsNullOrWhiteSpace(SearchText)
                    ? controllers
                    : controllers.Where(s =>
                        s.ServiceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                Services.Clear();
                foreach (var svc in filtered.OrderBy(s => s.DisplayName))
                {
                    var info = new ServiceInfo
                    {
                        ServiceName = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        Status = svc.Status
                    };

                    // Try to get metrics from C++ engine
                    if (svc.Status == ServiceControllerStatus.Running)
                    {
                        var response = await _pipeClient.SendCommandAsync(new IpcRequest
                        {
                            Command = "GET_STATUS",
                            TargetService = svc.ServiceName
                        });

                        if (response is not null)
                        {
                            info.CpuUsage = response.Cpu;
                            info.MemoryMB = response.MemoryMB;
                            info.UptimeSeconds = response.UptimeSeconds;
                        }
                    }

                    Services.Add(info);
                }
            }
            catch
            {
                Services.Clear();
            }
        }

        [RelayCommand]
        private async Task StartServiceAsync()
        {
            if (SelectedService is null) return;

            try
            {
                using var sc = new ServiceController(SelectedService.ServiceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                _snackbar.Show("Service Started",
                    $"{SelectedService.DisplayName} is now running.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Play24),
                    TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to start service {ServiceName}", SelectedService.ServiceName);
                _snackbar.Show("Start Failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
            }

            await RefreshServicesAsync();
        }

        [RelayCommand]
        private async Task StopServiceAsync()
        {
            if (SelectedService is null) return;

            try
            {
                using var sc = new ServiceController(SelectedService.ServiceName);
                if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                _snackbar.Show("Service Stopped",
                    $"{SelectedService.DisplayName} has been stopped.",
                    ControlAppearance.Caution,
                    new SymbolIcon(SymbolRegular.Stop24),
                    TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to stop service {ServiceName}", SelectedService.ServiceName);
                _snackbar.Show("Stop Failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
            }

            await RefreshServicesAsync();
        }

        [RelayCommand]
        private async Task RestartServiceAsync()
        {
            if (SelectedService is null) return;

            try
            {
                using var sc = new ServiceController(SelectedService.ServiceName);
                if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                _snackbar.Show("Service Restarted",
                    $"{SelectedService.DisplayName} has been restarted.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.ArrowSync24),
                    TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restart service {ServiceName}", SelectedService.ServiceName);
                _snackbar.Show("Restart Failed",
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
            }

            await RefreshServicesAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = RefreshServicesAsync();
        }
    }
}
