using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Services;
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

    public ICommand RestoreDefaultsCommand { get; }

    public ICommand RestoreFilesCommand { get; }

    public ICommand CloseCommand { get; }

    public LoadingScreenViewModel(Window currentWindow, Action? onLoadingScreenStatusChanged = null)
    {
        _currentWindow = currentWindow;
        _settings = AppSettings.Load();
        _gamePath = _settings.GamePath;
        _onLoadingScreenStatusChanged = onLoadingScreenStatusChanged;

        EnsureDefaultsFromAssets();

        SelectGamePathCommand = ReactiveCommand.Create(SelectGamePath);
        ReplaceFilesCommand = ReactiveCommand.Create(ReplaceFiles);
        RestoreDefaultsCommand = ReactiveCommand.Create(RestoreDefaults);
        RestoreFilesCommand = ReactiveCommand.Create(RestoreFiles);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    private static string AssetsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "BattleLoadingScreens");

    private static void EnsureDefaultsFromAssets() =>
        LoadingScreenPatch.EnsureDefaultsStored(AssetsDirectory);

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
        if (!TryValidateGamePath(out var requiredFolders))
            return;

        try
        {
            EnsureDefaultsFromAssets();

            var backupPath = LoadingScreenPatch.BackupPath;
            var backupExists = Directory.Exists(backupPath);

            if (!backupExists)
            {
                Directory.CreateDirectory(backupPath);

                foreach (var fileName in LoadingScreenPatch.DefaultFileNames)
                {
                    var target = LoadingScreenPatch.GetGameTargetPath(GamePath, fileName);
                    if (File.Exists(target))
                        File.Copy(target, Path.Combine(backupPath, fileName), true);
                }
            }

            ApplyDefaultsToGame();

            var optionalFont = Path.Combine(LoadingScreenPatch.DefaultsPath, "Statistics-Reader.ttf.dvpl");
            if (File.Exists(optionalFont))
            {
                File.Copy(
                    optionalFont,
                    LoadingScreenPatch.GetGameTargetPath(GamePath, "Statistics-Reader.ttf.dvpl"),
                    true);
            }

            ShowInfoMessage(
                backupExists
                    ? "Файлы экрана загрузки успешно обновлены!"
                    : "Файлы экрана загрузки успешно заменены!");
            _onLoadingScreenStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Произошла ошибка при замене файлов: {ex.Message}");
        }
    }

    private void RestoreDefaults()
    {
        if (!TryValidateGamePath(out _))
            return;

        try
        {
            EnsureDefaultsFromAssets();
            ApplyDefaultsToGame();

            var deletingFontPath = LoadingScreenPatch.GetGameTargetPath(GamePath, "Statistics-Reader.ttf.dvpl");
            if (File.Exists(deletingFontPath))
                File.Delete(deletingFontPath);

            var backupPath = LoadingScreenPatch.BackupPath;
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, true);

            ShowInfoMessage("Файлы по умолчанию успешно восстановлены!");
            _onLoadingScreenStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Произошла ошибка при восстановлении файлов по умолчанию: {ex.Message}");
        }
    }

    private void ApplyDefaultsToGame()
    {
        foreach (var fileName in LoadingScreenPatch.DefaultFileNames)
        {
            var sourceFile = Path.Combine(LoadingScreenPatch.DefaultsPath, fileName);
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException($"Не найден файл по умолчанию: {fileName}", sourceFile);

            File.Copy(sourceFile, LoadingScreenPatch.GetGameTargetPath(GamePath, fileName), true);
        }
    }

    private bool TryValidateGamePath(out string[] requiredFolders)
    {
        requiredFolders =
        [
            Path.Combine(GamePath, "Data", "Fonts"),
            Path.Combine(GamePath, "Data", "UI", "Screens3"),
            Path.Combine(GamePath, "Data", "UI", "Screens", "Battle")
        ];

        if (string.IsNullOrWhiteSpace(GamePath))
        {
            ShowErrorMessage("Пожалуйста, укажите путь к папке с игрой.");
            return false;
        }

        if (!Directory.Exists(GamePath))
        {
            ShowErrorMessage("Указанная папка не существует.");
            return false;
        }

        foreach (var folder in requiredFolders)
        {
            if (Directory.Exists(folder))
                continue;

            ShowErrorMessage($"Не найдена папка: {folder}");
            return false;
        }

        return true;
    }

    private void RestoreFiles()
    {
        var backupPath = LoadingScreenPatch.BackupPath;

        if (!Directory.Exists(backupPath))
        {
            ShowErrorMessage("Резервные копии не найдены.");
            return;
        }

        try
        {
            foreach (var backupFile in Directory.GetFiles(backupPath))
            {
                var fileName = Path.GetFileName(backupFile);
                if (!LoadingScreenPatch.DefaultFileNames.Contains(fileName))
                    continue;

                File.Copy(backupFile, LoadingScreenPatch.GetGameTargetPath(GamePath, fileName), true);
            }

            var deletingFontPath = LoadingScreenPatch.GetGameTargetPath(GamePath, "Statistics-Reader.ttf.dvpl");
            if (File.Exists(deletingFontPath))
            {
                File.Delete(deletingFontPath);
            }

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
