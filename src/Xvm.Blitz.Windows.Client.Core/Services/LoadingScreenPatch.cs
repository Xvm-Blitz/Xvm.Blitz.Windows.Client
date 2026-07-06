using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public static class LoadingScreenPatch
{
    public const string BackupFolderName = "Backup Loading Screen";

    public const string DefaultsFolderName = "Default Loading Screen";

    public static readonly string[] DefaultFileNames =
    [
        "Font.style.dvpl",
        "BattleLoadingScreen.yaml.dvpl"
    ];

    public static string AppDataRoot =>
        Path.GetDirectoryName(AppSettings.SettingsPath)!;

    public static string BackupPath =>
        Path.Combine(AppDataRoot, BackupFolderName);

    public static string DefaultsPath =>
        Path.Combine(AppDataRoot, DefaultsFolderName);

    public static bool IsReplaced =>
        Directory.Exists(BackupPath) &&
        DefaultFileNames.All(fileName => File.Exists(Path.Combine(BackupPath, fileName)));

    public static void EnsureDefaultsStored(string assetsDirectory)
    {
        Directory.CreateDirectory(DefaultsPath);

        foreach (var fileName in DefaultFileNames)
        {
            var assetFile = Path.Combine(assetsDirectory, fileName);
            var defaultFile = Path.Combine(DefaultsPath, fileName);

            if (!File.Exists(assetFile))
                continue;

            File.Copy(assetFile, defaultFile, true);
        }

        var optionalFontAsset = Path.Combine(assetsDirectory, "Statistics-Reader.ttf.dvpl");
        if (File.Exists(optionalFontAsset))
        {
            File.Copy(
                optionalFontAsset,
                Path.Combine(DefaultsPath, "Statistics-Reader.ttf.dvpl"),
                true);
        }
    }

    public static string GetGameTargetPath(string gamePath, string fileName) =>
        fileName switch
        {
            "Font.style.dvpl" => Path.Combine(gamePath, "Data", "UI", "Screens3", fileName),
            "BattleLoadingScreen.yaml.dvpl" => Path.Combine(gamePath, "Data", "UI", "Screens", "Battle", fileName),
            "Statistics-Reader.ttf.dvpl" => Path.Combine(gamePath, "Data", "Fonts", fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, null)
        };
}
