namespace WinServiceController.Models
{
    public class UserSettings
    {
        public string Theme { get; set; } = "System";
        public bool MinimizeToTray { get; set; } = true;
        public bool SuppressTrayNotification { get; set; }
        public bool StartWithWindows { get; set; }
        public string CppServiceExePath { get; set; } = string.Empty;

        // Chart settings
        public int ChartWindowSeconds { get; set; } = 7200;
        public int ChartCpuYMax { get; set; } = 100;
        public int ChartMemoryYMax { get; set; } = 0;   // 0 = auto
        public int ChartYMarginPercent { get; set; } = 10;
    }
}
