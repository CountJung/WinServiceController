using System.Text.Json.Serialization;

namespace WinServiceController.Models
{
    public class IpcRequest
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("targetService")]
        public string TargetService { get; set; } = string.Empty;
    }

    public class IpcResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("cpu")]
        public double Cpu { get; set; }

        [JsonPropertyName("memoryMB")]
        public double MemoryMB { get; set; }

        [JsonPropertyName("uptimeSeconds")]
        public long UptimeSeconds { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
