using System.Text.Json;
using System.Text.Json.Serialization;
using Xvm.Blitz.Windows.Client.Core.Helpers;

namespace Xvm.Blitz.Windows.Client.Core.Settings;

public sealed class AppSettings
{
    public static readonly string SettingsPath = Path.Combine(AppDataPaths.AppFolder, "settings.json");

    [JsonPropertyName("replay_path")]
    public string ReplaysPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TanksBlitz",
        "replays");

    [JsonPropertyName("allies_window_x")]
    public int AlliesWindowX { get; set; } = 0;

    [JsonPropertyName("allies_window_y")]
    public int AlliesWindowY { get; set; } = 0;

    [JsonPropertyName("enemies_window_x")]
    public int EnemiesWindowX { get; set; } = 1250;

    [JsonPropertyName("enemies_window_y")]
    public int EnemiesWindowY { get; set; } = 0;

    [JsonPropertyName("session_summary_overlay_x")]
    public int SessionSummaryOverlayX { get; set; } = 520;

    [JsonPropertyName("session_summary_overlay_y")]
    public int SessionSummaryOverlayY { get; set; } = 80;

    [JsonPropertyName("session_summary_overlay_visible")]
    public bool SessionSummaryOverlayVisible { get; set; }

    [JsonPropertyName("api_base_url")]
    public string ApiBaseUrl { get; set; } = "https://localhost:7206/api/";

    [JsonPropertyName("minimize_to_tray_on_close")]
    public bool MinimizeToTrayOnClose { get; set; } = true;

    [JsonPropertyName("game_path")]
    public string GamePath { get; set; } = string.Empty;

    [JsonPropertyName("has_seen_tutorial")]
    public bool HasSeenTutorial { get; set; }

    [JsonPropertyName("session_nickname")]
    public string SessionNickname { get; set; } = string.Empty;

    [JsonPropertyName("selected_session_id")]
    public Guid? SelectedSessionId { get; set; }

    [JsonPropertyName("panel_scale_x")]
    public double PanelScaleX { get; set; } = 1;

    [JsonPropertyName("panel_scale_y")]
    public double PanelScaleY { get; set; } = 1;

    [JsonPropertyName("session_summary_overlay_scale_x")]
    public double SessionSummaryOverlayScaleX { get; set; } = 1;

    [JsonPropertyName("session_summary_overlay_scale_y")]
    public double SessionSummaryOverlayScaleY { get; set; } = 1;

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
