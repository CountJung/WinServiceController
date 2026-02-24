using Microsoft.Win32;
using WinServiceController.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;

namespace WinServiceController.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "WinServiceController";

        private readonly IUserSettingsService _settingsService;
        private bool _isInitialized;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private bool _minimizeToTray;

        [ObservableProperty]
        private bool _suppressTrayNotification;

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private string _cppServiceExePath = string.Empty;

        public SettingsViewModel(IUserSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

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

            var s = _settingsService.Settings;
            MinimizeToTray = s.MinimizeToTray;
            SuppressTrayNotification = s.SuppressTrayNotification;
            StartWithWindows = s.StartWithWindows;
            CppServiceExePath = s.CppServiceExePath;

            _isInitialized = true;
        }

        private static string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? string.Empty;
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
            _settingsService.Settings.MinimizeToTray = value;
            _settingsService.Save();
        }

        partial void OnSuppressTrayNotificationChanged(bool value)
        {
            _settingsService.Settings.SuppressTrayNotification = value;
            _settingsService.Save();
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            _settingsService.Settings.StartWithWindows = value;
            SetAutoStart(value);
            _settingsService.Save();
        }

        partial void OnCppServiceExePathChanged(string value)
        {
            _settingsService.Settings.CppServiceExePath = value;
            _settingsService.Save();
        }

        [RelayCommand]
        private void BrowseCppServicePath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable|ServiceMonitorCore.exe|All files|*.*",
                Title = "Select ServiceMonitorCore.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                CppServiceExePath = dialog.FileName;
            }
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
                    _settingsService.Settings.Theme = "Light";
                    _settingsService.Save();
                    break;

                case "theme_dark":
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    UnwatchSystemTheme();
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    _settingsService.Settings.Theme = "Dark";
                    _settingsService.Save();
                    break;

                case "theme_system":
                    if (CurrentTheme == ApplicationTheme.Unknown)
                        break;

                    WatchSystemTheme();
                    ApplySystemTheme();
                    CurrentTheme = ApplicationTheme.Unknown;
                    _settingsService.Settings.Theme = "System";
                    _settingsService.Save();
                    break;
            }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key is null) return;

                if (enable)
                {
                    var exePath = Environment.ProcessPath ?? string.Empty;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue(StartupValueName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(StartupValueName, false);
                }
            }
            catch
            {
                // Registry access may fail in some environments
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
