using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows.Data;
using WinServiceController.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace WinServiceController.Views.Pages
{
    public partial class ChartPage : INavigableView<ChartViewModel>
    {
        public ChartViewModel ViewModel { get; }

        public ChartPage(ChartViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            var cpuChart = new CartesianChart();
            cpuChart.SetBinding(CartesianChart.SeriesProperty, new Binding("ViewModel.CpuSeries"));
            cpuChart.SetBinding(CartesianChart.XAxesProperty, new Binding("ViewModel.SharedXAxes"));
            cpuChart.SetBinding(CartesianChart.YAxesProperty, new Binding("ViewModel.CpuYAxes"));
            CpuChartHost.Child = cpuChart;

            var memChart = new CartesianChart();
            memChart.SetBinding(CartesianChart.SeriesProperty, new Binding("ViewModel.MemorySeries"));
            memChart.SetBinding(CartesianChart.XAxesProperty, new Binding("ViewModel.SharedXAxes"));
            memChart.SetBinding(CartesianChart.YAxesProperty, new Binding("ViewModel.MemoryYAxes"));
            MemoryChartHost.Child = memChart;
        }
    }
}
