namespace WinServiceController.Models
{
    public class AppConfig
    {
        public string ConfigurationsFolder { get; set; } = string.Empty;

        public string AppPropertiesFileName { get; set; } = string.Empty;

        public string PipeName { get; set; } = @"\\.\pipe\ServiceMonitorPipe";

        public int MonitoringIntervalMs { get; set; } = 1000;
    }
}
