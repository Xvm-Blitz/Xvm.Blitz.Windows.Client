using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class LoadingScreenViewModel : ReactiveObject
{
    private string _gamePath = string.Empty;

    private string _message = string.Empty;

    private bool _isError = false;

    private readonly Window _currentWindow;
    private readonly AppSettings _settings;
    private readonly Action? _onLoadingScreenStatusChanged;

    public string GamePath
    {
        get => _gamePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _gamePath, value);
            _settings.GamePath = value;
            AppSettings.Save(_settings);
        }
    }

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public bool IsError
    {
        get => _isError;
        set => this.RaiseAndSetIfChanged(ref _isError, value);
    }

    public ICommand SelectGamePathCommand { get; }

    public ICommand ReplaceFilesCommand { get; }

    public ICommand RestoreFilesCommand { get; }

    public ICommand CloseCommand { get; }

    public LoadingScreenViewModel(Window currentWindow, Action? onLoadingScreenStatusChanged = null)
    {
        _currentWindow = currentWindow;
        _settings = AppSettings.Load();
        _gamePath = _settings.GamePath;
        _onLoadingScreenStatusChanged = onLoadingScreenStatusChanged;

        SelectGamePathCommand = ReactiveCommand.Create(SelectGamePath);
        ReplaceFilesCommand = ReactiveCommand.Create(ReplaceFiles);
        RestoreFilesCommand = ReactiveCommand.Create(RestoreFiles);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    private async void SelectGamePath()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(_currentWindow);
            if (topLevel == null)
            {
                return;
            }

            var folderDialog = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Выберите папку с игрой Tanks Blitz",
                    AllowMultiple = false
                });

            if (folderDialog.Count > 0)
            {
                GamePath = folderDialog[0].TryGetLocalPath() ?? GamePath;
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Ошибка при выборе папки: {ex.Message}");
        }
    }

    private void ReplaceFiles()
    {
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            ShowErrorMessage("Пожалуйста, укажите путь к папке с игрой.");

            return;
        }

        if (!Directory.Exists(GamePath))
        {
            ShowErrorMessage("Указанная папка не существует.");

            return;
        }

        var requiredFolders = new[]
        {
            Path.Combine(GamePath, "Data", "Fonts"),
            Path.Combine(
                GamePath,
                "Data",
                "UI",
                "Screens3"),
            Path.Combine(
                GamePath,
                "Data",
                "UI",
                "Screens",
                "Battle")
        };

        foreach (var folder in requiredFolders)
        {
            if (Directory.Exists(folder))
                continue;

            ShowErrorMessage($"Не найдена папка: {folder}");

            return;
        }

        try
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(AppSettings.SettingsPath)!,
                "Backup Loading Screen");

            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }

            Directory.CreateDirectory(backupPath);

            var filesToReplace = new[]
            {
                (Source: "Font.style.dvpl", Target: Path.Combine(
                    GamePath,
                    "Data",
                    "UI",
                    "Screens3",
                    "Font.style.dvpl")),
                (Source: "BattleLoadingScreen.yaml.dvpl", Target: Path.Combine(
                    GamePath,
                    "Data",
                    "UI",
                    "Screens",
                    "Battle",
                    "BattleLoadingScreen.yaml.dvpl")),
            };

            var filesToCopy = new[]
            {
                (Source: "Statistics-Reader.ttf.dvpl", Target: Path.Combine(
                    GamePath,
                    "Data",
                    "Fonts",
                    "Statistics-Reader.ttf.dvpl"))
            }.Union(filesToReplace);

            foreach (var (source, target) in filesToCopy)
            {
                if (File.Exists(target) && filesToReplace.Contains((source, target)))
                {
                    var backupFile = Path.Combine(backupPath, Path.GetFileName(target));
                    File.Copy(target, backupFile, true);
                }

                var sourceFile = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "BattleLoadingScreens",
                    source);

                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, target, true);
                }
            }

            ShowInfoMessage("Файлы экрана загрузки успешно заменены!");
            _onLoadingScreenStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Произошла ошибка при замене файлов: {ex.Message}");
        }
    }

    private void RestoreFiles()
    {
        var backupPath = Path.Combine(
            Path.GetDirectoryName(AppSettings.SettingsPath)!,
            "Backup Loading Screen");

        if (!Directory.Exists(backupPath))
        {
            ShowErrorMessage("Резервные копии не найдены.");

            return;
        }

        try
        {
            var backupFiles = Directory.GetFiles(backupPath);

            foreach (var backupFile in backupFiles)
            {
                var fileName = Path.GetFileName(backupFile);
                string targetPath;

                switch (fileName)
                {
                    case "Font.style.dvpl":
                        targetPath = Path.Combine(
                            GamePath,
                            "Data",
                            "UI",
                            "Screens3",
                            fileName);
                        break;
                    case "BattleLoadingScreen.yaml.dvpl":
                        targetPath = Path.Combine(
                            GamePath,
                            "Data",
                            "UI",
                            "Screens",
                            "Battle",
                            fileName);
                        break;
                    default:
                        continue;
                }

                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, targetPath, true);
                }
            }

            var deletingFontPath = Path.Combine(
                GamePath,
                "Data",
                "Fonts",
                "Statistics-Reader.ttf.dvpl");

            File.Delete(deletingFontPath);

            Directory.Delete(backupPath, true);
            ShowInfoMessage("Файлы успешно восстановлены из резервной копии!");
            _onLoadingScreenStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Произошла ошибка при восстановлении файлов: {ex.Message}");
        }
    }

    private void Close()
    {
        _currentWindow.Close();
    }

    private void ShowErrorMessage(string message)
    {
        Message = message;
        IsError = true;
    }

    private void ShowInfoMessage(string message)
    {
        Message = message;
        IsError = false;
    }
}
