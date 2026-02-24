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
    }
}
