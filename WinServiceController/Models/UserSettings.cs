namespace WinServiceController.Models
{
    public class UserSettings
    {
        public string Theme { get; set; } = "System";
        public bool MinimizeToTray { get; set; } = true;
        public bool SuppressTrayNotification { get; set; }
        public bool StartWithWindows { get; set; }
        public string CppServiceExePath { get; set; } = string.Empty;
    }
}
