using WinServiceController.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

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
        }
    }
}
