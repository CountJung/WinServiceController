using System.ComponentModel;
using System.Windows.Forms;
using WinServiceController.Services;
using WinServiceController.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinServiceController.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly IUserSettingsService _settings;
        private readonly NotifyIcon _notifyIcon;
        private bool _forceClose;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService,
            IUserSettingsService settingsService
        )
        {
            ViewModel = viewModel;
            _settings = settingsService;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            // System tray icon
            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "wpfui-icon.ico")),
                Text = "Service Monitor",
                Visible = false
            };
            _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Open", null, (_, _) => RestoreFromTray());
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) =>
            {
                _forceClose = true;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    System.Windows.Application.Current.Shutdown());
            });
        }

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
            RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void SetServiceProvider(IServiceProvider serviceProvider) { }

        public void ShowWindow() => Show();

        public void CloseWindow()
        {
            _forceClose = true;
            Close();
        }

        public void ForceExit()
        {
            _forceClose = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Close();
        }

        private void RestoreFromTray()
        {
            _notifyIcon.Visible = false;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_forceClose && _settings.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                _notifyIcon.Visible = true;

                if (!_settings.Settings.SuppressTrayNotification)
                {
                    _notifyIcon.ShowBalloonTip(
                        2000,
                        "Service Monitor",
                        "Application minimized to system tray.",
                        ToolTipIcon.Info);
                }

                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.OnClosed(e);
            System.Windows.Application.Current.Shutdown();
        }
    }
}
