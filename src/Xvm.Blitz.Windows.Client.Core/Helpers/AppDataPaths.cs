namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class AppDataPaths
{
    public const string AppFolderName = "XvmBlitz";

    public static string AppFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);
}
