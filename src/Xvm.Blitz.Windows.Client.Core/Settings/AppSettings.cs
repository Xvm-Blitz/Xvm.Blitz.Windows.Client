using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Settings;

public sealed class AppSettings
{
    public static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XvmBlitzStatistics",
        "settings.json");

    [JsonPropertyName("replay_path")]
    public string ReplaysPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TanksBlitz",
        "replays");

    [JsonPropertyName("hide_statistics_hotkey")]
    public string HideStatisticsHotkey { get; set; } = "H";

    [JsonPropertyName("hide_statistics_ctrl")]
    public bool HideStatisticsCtrl { get; set; } = true;

    [JsonPropertyName("hide_statistics_alt")]
    public bool HideStatisticsAlt { get; set; }

    [JsonPropertyName("hide_statistics_shift")]
    public bool HideStatisticsShift { get; set; }

    [JsonPropertyName("allies_window_x")]
    public int AlliesWindowX { get; set; } = 0;

    [JsonPropertyName("allies_window_y")]
    public int AlliesWindowY { get; set; } = 0;

    [JsonPropertyName("enemies_window_x")]
    public int EnemiesWindowX { get; set; } = 1250;

    [JsonPropertyName("enemies_window_y")]
    public int EnemiesWindowY { get; set; } = 0;

    [JsonPropertyName("api_base_url")]
    public string ApiBaseUrl { get; set; } = "https://xvmblitz.ru/api/";

    [JsonPropertyName("minimize_to_tray_on_close")]
    public bool MinimizeToTrayOnClose { get; set; } = true;

    [JsonPropertyName("game_path")]
    public string GamePath { get; set; } = string.Empty;

    [JsonPropertyName("has_seen_tutorial")]
    public bool HasSeenTutorial { get; set; }

    [JsonPropertyName("panel_scale_x")]
    public double PanelScaleX { get; set; } = 1;

    [JsonPropertyName("panel_scale_y")]
    public double PanelScaleY { get; set; } = 1;

    public static AppSettings Load()
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(SettingsPath)!;

            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            MigrateLegacyPanelScales(settings, json);
            return settings;
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    private static void MigrateLegacyPanelScales(AppSettings settings, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var hasSharedScale = root.TryGetProperty("panel_scale_x", out _);
            if (hasSharedScale)
                return;

            if (root.TryGetProperty("allies_panel_scale_x", out var alliesScaleX))
                settings.PanelScaleX = alliesScaleX.GetDouble();

            if (root.TryGetProperty("allies_panel_scale_y", out var alliesScaleY))
                settings.PanelScaleY = alliesScaleY.GetDouble();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(SettingsPath)!;

            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
