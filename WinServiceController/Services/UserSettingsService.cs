using System.IO;
using System.Text.Json;
using WinServiceController.Models;

namespace WinServiceController.Services
{
    public interface IUserSettingsService
    {
        UserSettings Settings { get; }
        void Save();
        void Load();
    }

    public class UserSettingsService : IUserSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            AppContext.BaseDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public UserSettings Settings { get; private set; } = new();

        public UserSettingsService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    Settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new();
                }
            }
            catch
            {
                Settings = new();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail — settings are non-critical
            }
        }
    }
}
