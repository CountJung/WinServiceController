using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WinServiceController.Models;
using WinServiceController.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class ServiceListViewModel : ObservableObject, INavigationAware
    {
        private const int TopNDefault = 10;

        private static readonly SKColor[] Palette =
        [
            SKColors.DodgerBlue, SKColors.OrangeRed, SKColors.MediumSeaGreen,
            SKColors.MediumPurple, SKColors.Goldenrod, SKColors.DeepPink,
            SKColors.Teal, SKColors.Coral, SKColors.SlateBlue, SKColors.Crimson,
            SKColors.DarkCyan, SKColors.IndianRed, SKColors.Olive, SKColors.SteelBlue,
            SKColors.Orchid, SKColors.Sienna, SKColors.CadetBlue, SKColors.Tomato,
            SKColors.RoyalBlue, SKColors.PaleVioletRed
        ];

        private readonly IPipeClientService _pipeClient;
        private readonly ISnackbarService _snackbar;
        private readonly IUserSettingsService _settingsService;
        private bool _isInitialized;
        private DispatcherTimer? _pollTimer;
        private int _tickCount;
        private bool _hasAutoSelectedTop10;

        // Per-service chart data
        private readonly Dictionary<string, ServiceChartData> _chartData = [];
        private int _nextColor;

        // --- Service List ---

        [ObservableProperty]
        private ObservableCollection<ServiceInfo> _services = [];

        [ObservableProperty]
        private ServiceInfo? _selectedService;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // --- Chart ---

        [ObservableProperty]
        private ObservableCollection<ISeries> _cpuSeries = [];

        [ObservableProperty]
        private ObservableCollection<ISeries> _memorySeries = [];

        [ObservableProperty]
        private Axis[] _cpuYAxes = [new Axis { Name = "CPU %", MinLimit = 0, MaxLimit = 100 }];

        [ObservableProperty]
        private Axis[] _memoryYAxes = [new Axis { Name = "Memory MB", MinLimit = 0 }];

        [ObservableProperty]
        private Axis[] _cpuXAxes =
        [
            new Axis { Name = "Time (s)", MinLimit = 0, MaxLimit = 7200, ShowSeparatorLines = false }
        ];

        [ObservableProperty]
        private Axis[] _memoryXAxes =
        [
            new Axis { Name = "Time (s)", MinLimit = 0, MaxLimit = 7200, ShowSeparatorLines = false }
        ];

        [ObservableProperty]
        private string _chartTitle = "Resource Monitor (all running services)";

        // --- Delete confirmation ---

        [ObservableProperty]
        private bool _isDeleteDialogOpen;

        public ServiceListViewModel(
            IPipeClientService pipeClient,
            ISnackbarService snackbarService,
            IUserSettingsService settingsService)
        {
            _pipeClient = pipeClient;
            _snackbar = snackbarService;
            _settingsService = settingsService;

            ApplyChartSettings();
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _pollTimer.Tick += async (_, _) => await PollAllServicesAsync();
            }

            ApplyChartSettings();
            await RefreshServicesAsync();
            _pollTimer?.Start();
        }

        public Task OnNavigatedFromAsync()
        {
            _pollTimer?.Stop();
            return Task.CompletedTask;
        }

        private void ApplyChartSettings()
        {
            var s = _settingsService.Settings;
            var window = s.ChartWindowSeconds > 0 ? s.ChartWindowSeconds : 7200;
            var margin = Math.Clamp(s.ChartYMarginPercent, 0, 50);

            CpuXAxes[0].MaxLimit = window;
            MemoryXAxes[0].MaxLimit = window;

            var cpuMax = s.ChartCpuYMax > 0 ? s.ChartCpuYMax : 100;
            CpuYAxes[0].MaxLimit = cpuMax + cpuMax * margin / 100.0;
            CpuYAxes[0].MinLimit = 0;

            if (s.ChartMemoryYMax > 0)
            {
                MemoryYAxes[0].MaxLimit = s.ChartMemoryYMax + s.ChartMemoryYMax * margin / 100.0;
                MemoryYAxes[0].MinLimit = 0;
            }
            else
            {
                MemoryYAxes[0].MaxLimit = null;
                MemoryYAxes[0].MinLimit = 0;
            }
        }

        // --- ShowInChart toggle handler ---

        private void OnServiceShowInChartChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ServiceInfo.ShowInChart) || sender is not ServiceInfo info)
                return;

            if (!_chartData.TryGetValue(info.ServiceName, out var cd))
                return;

            if (info.ShowInChart)
            {
                if (!CpuSeries.Contains(cd.CpuLine))
                    CpuSeries.Add(cd.CpuLine);
                if (!MemorySeries.Contains(cd.MemLine))
                    MemorySeries.Add(cd.MemLine);
            }
            else
            {
                CpuSeries.Remove(cd.CpuLine);
                MemorySeries.Remove(cd.MemLine);
            }
        }

        // --- Service List Commands ---

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            // Unsubscribe old handlers
            foreach (var svc in Services)
                svc.PropertyChanged -= OnServiceShowInChartChanged;

            try
            {
                var controllers = ServiceController.GetServices();
                var filtered = string.IsNullOrWhiteSpace(SearchText)
                    ? controllers
                    : controllers.Where(s =>
                        s.ServiceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                var newList = new List<ServiceInfo>();
                foreach (var svc in filtered.OrderBy(s => s.DisplayName))
                {
                    var info = new ServiceInfo
                    {
                        ServiceName = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        Status = svc.Status
                    };

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

                    newList.Add(info);
                }

                // Auto-select top 10 by resource consumption on first load
                if (!_hasAutoSelectedTop10)
                {
                    _hasAutoSelectedTop10 = true;
                    var top = newList
                        .Where(s => s.Status == ServiceControllerStatus.Running)
                        .OrderByDescending(s => s.CpuUsage + s.MemoryMB)
                        .Take(TopNDefault)
                        .Select(s => s.ServiceName)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var info in newList)
                        info.ShowInChart = top.Contains(info.ServiceName);
                }
                else
                {
                    // Preserve existing ShowInChart state across refresh
                    var existing = Services.ToDictionary(
                        s => s.ServiceName, s => s.ShowInChart, StringComparer.OrdinalIgnoreCase);
                    foreach (var info in newList)
                    {
                        if (existing.TryGetValue(info.ServiceName, out var show))
                            info.ShowInChart = show;
                    }
                }

                Services.Clear();
                foreach (var info in newList)
                {
                    info.PropertyChanged += OnServiceShowInChartChanged;
                    Services.Add(info);
                }

                SyncChartVisibility();
            }
            catch
            {
                Services.Clear();
            }
        }

        private void SyncChartVisibility()
        {
            var visible = Services
                .Where(s => s.ShowInChart)
                .Select(s => s.ServiceName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, cd) in _chartData)
            {
                var shouldShow = visible.Contains(name);
                var isShown = CpuSeries.Contains(cd.CpuLine);

                if (shouldShow && !isShown)
                {
                    CpuSeries.Add(cd.CpuLine);
                    MemorySeries.Add(cd.MemLine);
                }
                else if (!shouldShow && isShown)
                {
                    CpuSeries.Remove(cd.CpuLine);
                    MemorySeries.Remove(cd.MemLine);
                }
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
                _snackbar.Show("Start Failed", ex.Message,
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
                _snackbar.Show("Stop Failed", ex.Message,
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
                _snackbar.Show("Restart Failed", ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
            }

            await RefreshServicesAsync();
        }

        [RelayCommand]
        private void RequestDeleteService()
        {
            if (SelectedService is null) return;
            IsDeleteDialogOpen = true;
        }

        [RelayCommand]
        private async Task ConfirmDeleteServiceAsync()
        {
            IsDeleteDialogOpen = false;
            if (SelectedService is null) return;

            var serviceName = SelectedService.ServiceName;
            var displayName = SelectedService.DisplayName;

            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }

                var (ok, output) = await RunScCommandAsync($"delete {serviceName}");
                if (ok)
                {
                    _snackbar.Show("Service Deleted",
                        $"{displayName} has been removed.",
                        ControlAppearance.Info,
                        new SymbolIcon(SymbolRegular.Delete24),
                        TimeSpan.FromSeconds(3));
                }
                else
                {
                    _snackbar.Show("Delete Failed", output,
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.ErrorCircle24),
                        TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to delete service {ServiceName}", serviceName);
                _snackbar.Show("Delete Failed", ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(5));
            }

            SelectedService = null;
            await RefreshServicesAsync();
        }

        [RelayCommand]
        private void CancelDeleteService()
        {
            IsDeleteDialogOpen = false;
        }

        [RelayCommand]
        private void ClearChart()
        {
            CpuSeries.Clear();
            MemorySeries.Clear();
            _chartData.Clear();
            _nextColor = 0;
            _tickCount = 0;

            var window = _settingsService.Settings.ChartWindowSeconds > 0
                ? _settingsService.Settings.ChartWindowSeconds : 7200;
            CpuXAxes[0].MinLimit = 0;
            CpuXAxes[0].MaxLimit = window;
            MemoryXAxes[0].MinLimit = 0;
            MemoryXAxes[0].MaxLimit = window;
        }

        // --- Multi-service polling ---

        private async Task PollAllServicesAsync()
        {
            var window = _settingsService.Settings.ChartWindowSeconds > 0
                ? _settingsService.Settings.ChartWindowSeconds : 7200;

            var response = await _pipeClient.SendCommandAsync(new IpcRequest
            {
                Command = "GET_ALL_STATUS"
            });

            if (response?.Services is { Count: > 0 })
            {
                foreach (var snap in response.Services)
                    AddDataPoint(snap.Name, snap.Cpu, snap.MemoryMB, window);
            }
            else
            {
                foreach (var svc in Services.Where(s => s.Status == ServiceControllerStatus.Running))
                {
                    var r = await _pipeClient.SendCommandAsync(new IpcRequest
                    {
                        Command = "GET_STATUS",
                        TargetService = svc.ServiceName
                    });

                    if (r is not null)
                        AddDataPoint(svc.ServiceName, r.Cpu, r.MemoryMB, window);
                }
            }

            _tickCount++;
            if (_tickCount > window)
            {
                CpuXAxes[0].MinLimit = _tickCount - window;
                CpuXAxes[0].MaxLimit = _tickCount;
                MemoryXAxes[0].MinLimit = _tickCount - window;
                MemoryXAxes[0].MaxLimit = _tickCount;
            }
        }

        private void AddDataPoint(string serviceName, double cpu, double memMB, int maxPoints)
        {
            if (!_chartData.TryGetValue(serviceName, out var cd))
            {
                var ci = _nextColor++ % Palette.Length;
                var color = Palette[ci];

                var cpuValues = new ObservableCollection<ObservableValue>();
                var memValues = new ObservableCollection<ObservableValue>();

                var cpuLine = new LineSeries<ObservableValue>
                {
                    Values = cpuValues,
                    Name = serviceName,
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(color, 2),
                    Fill = null,
                };

                var memLine = new LineSeries<ObservableValue>
                {
                    Values = memValues,
                    Name = serviceName,
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(color, 2),
                    Fill = null,
                };

                cd = new ServiceChartData(cpuValues, memValues, cpuLine, memLine);
                _chartData[serviceName] = cd;

                // Only add to visible chart if ShowInChart is checked
                var svcInfo = Services.FirstOrDefault(s =>
                    s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (svcInfo is null or { ShowInChart: true })
                {
                    CpuSeries.Add(cpuLine);
                    MemorySeries.Add(memLine);
                }
            }

            cd.CpuValues.Add(new ObservableValue(cpu));
            cd.MemValues.Add(new ObservableValue(memMB));

            while (cd.CpuValues.Count > maxPoints)
                cd.CpuValues.RemoveAt(0);
            while (cd.MemValues.Count > maxPoints)
                cd.MemValues.RemoveAt(0);
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = RefreshServicesAsync();
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

        private sealed record ServiceChartData(
            ObservableCollection<ObservableValue> CpuValues,
            ObservableCollection<ObservableValue> MemValues,
            LineSeries<ObservableValue> CpuLine,
            LineSeries<ObservableValue> MemLine);
    }
}
