using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Threading;
using WinServiceController.Models;
using WinServiceController.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private const string ServiceName = "ServiceMonitorCore";

        private readonly IPipeClientService _pipeClient;
        private readonly IUserSettingsService _settingsService;
        private readonly ISnackbarService _snackbar;
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

        [ObservableProperty]
        private bool _isEngineInstalled;

        [ObservableProperty]
        private bool _isEngineRunning;

        public DashboardViewModel(
            IPipeClientService pipeClient,
            IUserSettingsService settingsService,
            ISnackbarService snackbarService)
        {
            _pipeClient = pipeClient;
            _settingsService = settingsService;
            _snackbar = snackbarService;
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

        [RelayCommand]
        private async Task InstallEngineAsync()
        {
            var exePath = _settingsService.Settings.CppServiceExePath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                EngineStatus = "Set engine path in Settings first";
                _snackbar.Show("Configuration Required",
                    "Set the ServiceMonitorCore.exe path in Settings first.",
                    ControlAppearance.Caution,
                    new SymbolIcon(SymbolRegular.Warning24),
                    TimeSpan.FromSeconds(4));
                return;
            }

            var (ok, output) = await RunScCommandAsync($"create {ServiceName} binPath= \"{exePath}\" start= demand");
            if (ok)
            {
                // Configure auto-restart: restart after 5s on first/second/third failure
                await RunScCommandAsync($"failure {ServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000");
                _snackbar.Show("Engine Installed",
                    "Service registered with auto-restart on crash.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Checkmark24),
                    TimeSpan.FromSeconds(3));
            }
            else
            {
                _snackbar.Show("Install Failed", output,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(5));
            }

            await RefreshAllAsync();
        }

        [RelayCommand]
        private async Task UninstallEngineAsync()
        {
            var (ok, output) = await RunScCommandAsync($"delete {ServiceName}");
            _snackbar.Show(
                ok ? "Engine Uninstalled" : "Uninstall Failed",
                ok ? "Service removed successfully." : output,
                ok ? ControlAppearance.Info : ControlAppearance.Danger,
                new SymbolIcon(ok ? SymbolRegular.Delete24 : SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(3));
            await RefreshAllAsync();
        }

        [RelayCommand]
        private async Task StartEngineAsync()
        {
            var (ok, output) = await RunScCommandAsync($"start {ServiceName}");
            _snackbar.Show(
                ok ? "Engine Started" : "Start Failed",
                ok ? "Monitoring engine is now running." : output,
                ok ? ControlAppearance.Success : ControlAppearance.Danger,
                new SymbolIcon(ok ? SymbolRegular.Play24 : SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(3));
            await Task.Delay(1000);
            await RefreshAllAsync();
        }

        [RelayCommand]
        private async Task StopEngineAsync()
        {
            var (ok, output) = await RunScCommandAsync($"stop {ServiceName}");
            _snackbar.Show(
                ok ? "Engine Stopped" : "Stop Failed",
                ok ? "Monitoring engine stopped." : output,
                ok ? ControlAppearance.Caution : ControlAppearance.Danger,
                new SymbolIcon(ok ? SymbolRegular.Stop24 : SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(3));
            await Task.Delay(1000);
            await RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            RefreshServiceCounts();
            RefreshEngineServiceState();
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

        private void RefreshEngineServiceState()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                _ = sc.Status; // triggers InvalidOperationException if not installed
                IsEngineInstalled = true;
                IsEngineRunning = sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                IsEngineInstalled = false;
                IsEngineRunning = false;
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

                if (IsEngineConnected)
                    EngineStatus = "Connected (Pipe OK)";
                else if (IsEngineRunning)
                    EngineStatus = "Service running, pipe not ready";
                else if (IsEngineInstalled)
                    EngineStatus = "Installed — Stopped";
                else
                    EngineStatus = "Not installed";
            }
            catch
            {
                IsEngineConnected = false;
                EngineStatus = IsEngineInstalled
                    ? (IsEngineRunning ? "Running — Pipe error" : "Installed — Stopped")
                    : "Not installed";
            }
        }

        private static async Task<(bool Success, string Output)> RunScCommandAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };

                    using var process = Process.Start(psi);
                    if (process is null)
                        return (false, "Failed to start sc.exe");

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);

                    var output = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
                    return (process.ExitCode == 0, output);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "sc.exe command failed: {Args}", arguments);
                    return (false, ex.Message);
                }
            });
        }
    }
}
