using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.WPF;
using WinServiceController.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Binding = System.Windows.Data.Binding;

namespace WinServiceController.Views.Pages
{
    public partial class ServiceListPage : INavigableView<ServiceListViewModel>
    {
        public ServiceListViewModel ViewModel { get; }

        public ServiceListPage(ServiceListViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            // Wire up GridViewColumnHeader click for sorting
            ServiceListView.AddHandler(
                GridViewColumnHeader.ClickEvent,
                new RoutedEventHandler(OnColumnHeaderClick));

            // Create charts
            var cpuChart = new CartesianChart();
            cpuChart.SetBinding(CartesianChart.SeriesProperty, new Binding("ViewModel.CpuSeries"));
            cpuChart.SetBinding(CartesianChart.XAxesProperty, new Binding("ViewModel.CpuXAxes"));
            cpuChart.SetBinding(CartesianChart.YAxesProperty, new Binding("ViewModel.CpuYAxes"));
            CpuChartHost.Child = cpuChart;

            var memChart = new CartesianChart();
            memChart.SetBinding(CartesianChart.SeriesProperty, new Binding("ViewModel.MemorySeries"));
            memChart.SetBinding(CartesianChart.XAxesProperty, new Binding("ViewModel.MemoryXAxes"));
            memChart.SetBinding(CartesianChart.YAxesProperty, new Binding("ViewModel.MemoryYAxes"));
            MemoryChartHost.Child = memChart;
        }

        // --- Column header click → sorting ---

        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader header) return;
            if (header.Role == GridViewColumnHeaderRole.Padding) return;

            var headerText = header.Column?.Header?.ToString() ?? "";
            var columnName = MapHeaderToColumn(headerText);
            if (string.IsNullOrEmpty(columnName)) return;

            ViewModel.ToggleSortCommand.Execute(columnName);
        }

        private static string MapHeaderToColumn(string headerText)
        {
            var clean = headerText.Replace(" ▲", "").Replace(" ▼", "").Trim();

            return clean switch
            {
                "Chart" => "ShowInChart",
                "Display Name" => "DisplayName",
                "Service Name" => "ServiceName",
                "Status" => "Status",
                "CPU (%)" => "CpuUsage",
                "Memory (MB)" => "MemoryMB",
                _ => ""
            };
        }

        // --- Filter button click handlers (called from XAML) ---

        private void OnStatusFilterClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsCpuFilterOpen = false;
            ViewModel.IsMemFilterOpen = false;
            StatusFilterPopup.PlacementTarget = sender as UIElement;
            ViewModel.IsStatusFilterOpen = !ViewModel.IsStatusFilterOpen;
        }

        private void OnCpuFilterClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsStatusFilterOpen = false;
            ViewModel.IsMemFilterOpen = false;
            CpuFilterPopup.PlacementTarget = sender as UIElement;
            ViewModel.IsCpuFilterOpen = !ViewModel.IsCpuFilterOpen;
        }

        private void OnMemFilterClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsStatusFilterOpen = false;
            ViewModel.IsCpuFilterOpen = false;
            MemFilterPopup.PlacementTarget = sender as UIElement;
            ViewModel.IsMemFilterOpen = !ViewModel.IsMemFilterOpen;
        }
    }
}
