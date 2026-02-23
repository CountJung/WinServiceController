using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinServiceController.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"Service Monitor - {GetAssemblyVersion()}";
            _isInitialized = true;
        }

        private static string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? string.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    UnwatchSystemTheme();
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;
                    break;

                case "theme_dark":
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    UnwatchSystemTheme();
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    break;

                case "theme_system":
                    if (CurrentTheme == ApplicationTheme.Unknown)
                        break;

                    WatchSystemTheme();
                    ApplySystemTheme();
                    CurrentTheme = ApplicationTheme.Unknown;
                    break;
            }
        }

        private static void ApplySystemTheme()
        {
            var systemTheme = ApplicationThemeManager.GetSystemTheme();
            var appTheme = systemTheme is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(appTheme);
        }

        private static void WatchSystemTheme()
        {
            if (Application.Current.MainWindow is FluentWindow window)
                SystemThemeWatcher.Watch(window);
        }

        private static void UnwatchSystemTheme()
        {
            if (Application.Current.MainWindow is FluentWindow window)
                SystemThemeWatcher.UnWatch(window);
        }
    }
}
