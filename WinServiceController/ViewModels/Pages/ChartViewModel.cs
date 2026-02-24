using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Threading;
using WinServiceController.Models;
using WinServiceController.Services;
using Wpf.Ui.Abstractions.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class ChartViewModel : ObservableObject, INavigationAware
    {
        private const int MaxDataPoints = 300;

        private readonly IPipeClientService _pipeClient;
        private readonly ObservableCollection<ObservableValue> _cpuValues = [];
        private readonly ObservableCollection<ObservableValue> _memoryValues = [];
        private DispatcherTimer? _pollTimer;
        private bool _isInitialized;

        [ObservableProperty]
        private string _targetServiceName = "Spooler";

        [ObservableProperty]
        private ISeries[] _cpuSeries;

        [ObservableProperty]
        private ISeries[] _memorySeries;

        [ObservableProperty]
        private Axis[] _cpuYAxes =
        [
            new Axis { Name = "CPU %", MinLimit = 0, MaxLimit = 100 }
        ];

        [ObservableProperty]
        private Axis[] _memoryYAxes =
        [
            new Axis { Name = "Memory MB", MinLimit = 0 }
        ];

        [ObservableProperty]
        private Axis[] _sharedXAxes =
        [
            new Axis { Name = "Time", LabelsRotation = 0, ShowSeparatorLines = false }
        ];

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private string _lastCpu = "—";

        [ObservableProperty]
        private string _lastMemory = "—";

        public string ToggleButtonText => IsMonitoring ? "Stop" : "Start";

        partial void OnIsMonitoringChanged(bool value)
        {
            OnPropertyChanged(nameof(ToggleButtonText));
        }

        public ChartViewModel(IPipeClientService pipeClient)
        {
            _pipeClient = pipeClient;

            _cpuSeries =
            [
                new LineSeries<ObservableValue>
                {
                    Values = _cpuValues,
                    Name = "CPU %",
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(40)),
                }
            ];

            _memorySeries =
            [
                new LineSeries<ObservableValue>
                {
                    Values = _memoryValues,
                    Name = "Memory MB",
                    GeometrySize = 0,
                    LineSmoothness = 0.3,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(40)),
                }
            ];
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _pollTimer.Tick += async (_, _) => await PollMetricsAsync();
            }

            StartMonitoring();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            StopMonitoring();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void ToggleMonitoring()
        {
            if (IsMonitoring)
                StopMonitoring();
            else
                StartMonitoring();
        }

        [RelayCommand]
        private void ClearChart()
        {
            _cpuValues.Clear();
            _memoryValues.Clear();
            LastCpu = "—";
            LastMemory = "—";
        }

        private void StartMonitoring()
        {
            IsMonitoring = true;
            _pollTimer?.Start();
        }

        private void StopMonitoring()
        {
            IsMonitoring = false;
            _pollTimer?.Stop();
        }

        private async Task PollMetricsAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetServiceName))
                return;

            var response = await _pipeClient.SendCommandAsync(new IpcRequest
            {
                Command = "GET_STATUS",
                TargetService = TargetServiceName
            });

            if (response is null)
            {
                _cpuValues.Add(new ObservableValue(0));
                _memoryValues.Add(new ObservableValue(0));
                LastCpu = "N/A";
                LastMemory = "N/A";
            }
            else
            {
                _cpuValues.Add(new ObservableValue(response.Cpu));
                _memoryValues.Add(new ObservableValue(response.MemoryMB));
                LastCpu = $"{response.Cpu:F2} %";
                LastMemory = $"{response.MemoryMB:F1} MB";
            }

            // Trim to max data points
            while (_cpuValues.Count > MaxDataPoints)
                _cpuValues.RemoveAt(0);
            while (_memoryValues.Count > MaxDataPoints)
                _memoryValues.RemoveAt(0);
        }
    }
}
