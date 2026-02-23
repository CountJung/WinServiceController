using System.ServiceProcess;

namespace WinServiceController.Models
{
    public partial class ServiceInfo : ObservableObject
    {
        [ObservableProperty]
        private string _serviceName = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private ServiceControllerStatus _status;

        [ObservableProperty]
        private double _cpuUsage;

        [ObservableProperty]
        private double _memoryMB;

        [ObservableProperty]
        private long _uptimeSeconds;

        [ObservableProperty]
        private string _executablePath = string.Empty;

        public string StatusText => Status switch
        {
            ServiceControllerStatus.Running => "Running",
            ServiceControllerStatus.Stopped => "Stopped",
            ServiceControllerStatus.Paused => "Paused",
            ServiceControllerStatus.StartPending => "Starting",
            ServiceControllerStatus.StopPending => "Stopping",
            ServiceControllerStatus.ContinuePending => "Resuming",
            ServiceControllerStatus.PausePending => "Pausing",
            _ => "Unknown"
        };
    }
}
