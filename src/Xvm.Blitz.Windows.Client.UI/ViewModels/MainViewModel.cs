using System.Diagnostics;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.Windows;
using Windows_AuthorizationWindow = Xvm.Blitz.Windows.Client.UI.Windows.AuthorizationWindow;
using LoadingScreenWindow = Xvm.Blitz.Windows.Client.UI.Windows.LoadingScreenWindow;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class MainViewModel : ReactiveObject
{
    private static Windows_AuthorizationWindow? _currentAuthWindow;
    private static LoadingScreenWindow? _currentLoadingScreenWindow;

    private readonly IAuthorizationService _authorizationService;

    private readonly ILogger<MainViewModel> _logger;

    private readonly AppSettings _settings;

    private int _alliesWindowX;

    private int _alliesWindowY;

    private int _enemiesWindowX;

    private int _enemiesWindowY;

    private bool _hideStatisticsAlt;

    private bool _hideStatisticsCtrl;

    private string _hideStatisticsHotkey;

    private bool _hideStatisticsShift;

    private bool _isBattleWindowsVisible = true;

    private bool _isDisplayConfigurationMode;

    private bool _isWindowsVisible = true;

    private bool _minimizeToTrayOnClose;

    private int _originalAlliesWindowX;

    private int _originalAlliesWindowY;

    private int _originalEnemiesWindowX;

    private int _originalEnemiesWindowY;

    private string _screenshotsPath;

    private bool _isLoadingScreenReplaced;

    public string ScreenshotsPath
    {
        get => _screenshotsPath;
        set => this.RaiseAndSetIfChanged(ref _screenshotsPath, value);
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => this.RaiseAndSetIfChanged(ref _minimizeToTrayOnClose, value);
    }

    public string HideStatisticsHotkey
    {
        get => _hideStatisticsHotkey;
        set => this.RaiseAndSetIfChanged(ref _hideStatisticsHotkey, value);
    }

    public bool HideStatisticsCtrl
    {
        get => _hideStatisticsCtrl;
        set => this.RaiseAndSetIfChanged(ref _hideStatisticsCtrl, value);
    }

    public bool HideStatisticsAlt
    {
        get => _hideStatisticsAlt;
        set => this.RaiseAndSetIfChanged(ref _hideStatisticsAlt, value);
    }

    public bool HideStatisticsShift
    {
        get => _hideStatisticsShift;
        set => this.RaiseAndSetIfChanged(ref _hideStatisticsShift, value);
    }

    public string HotkeyDisplayText
    {
        get
        {
            var parts = new List<string>();
            if (HideStatisticsCtrl)
                parts.Add("Ctrl");

            if (HideStatisticsAlt)
                parts.Add("Alt");

            if (HideStatisticsShift)
                parts.Add("Shift");

            parts.Add(HideStatisticsHotkey);

            return string.Join(" + ", parts);
        }
    }

    public int AlliesWindowX
    {
        get => _alliesWindowX;
        set => this.RaiseAndSetIfChanged(ref _alliesWindowX, value);
    }

    public int AlliesWindowY
    {
        get => _alliesWindowY;
        set => this.RaiseAndSetIfChanged(ref _alliesWindowY, value);
    }

    public int EnemiesWindowX
    {
        get => _enemiesWindowX;
        set => this.RaiseAndSetIfChanged(ref _enemiesWindowX, value);
    }

    public int EnemiesWindowY
    {
        get => _enemiesWindowY;
        set => this.RaiseAndSetIfChanged(ref _enemiesWindowY, value);
    }

    public bool IsWindowsVisible
    {
        get => _isWindowsVisible;
        set => this.RaiseAndSetIfChanged(ref _isWindowsVisible, value);
    }

    public bool IsDisplayConfigurationMode
    {
        get => _isDisplayConfigurationMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, value);
            this.RaisePropertyChanged(nameof(CanEditCoordinates));
        }
    }

    public bool IsBattleWindowsVisible
    {
        get => _isBattleWindowsVisible;
        set => this.RaiseAndSetIfChanged(ref _isBattleWindowsVisible, value);
    }

    public bool ConfigurationModeWithAlreadyData { get; private set; }

    public bool CanEditCoordinates => IsDisplayConfigurationMode;

    public bool IsLoadingScreenReplaced
    {
        get => _isLoadingScreenReplaced;
        set => this.RaiseAndSetIfChanged(ref _isLoadingScreenReplaced, value);
    }

    public bool IsApiKeyExists => _authorizationService.IsApiKeyExists;

    public string AuthDisplayText => IsApiKeyExists ? "Профиль" : "Войти";

    public PixelPoint AlliesWindowPosition
    {
        get => new(AlliesWindowX, AlliesWindowY);
        set
        {
            AlliesWindowX = value.X;
            AlliesWindowY = value.Y;
        }
    }

    public PixelPoint EnemiesWindowPosition
    {
        get => new(EnemiesWindowX, EnemiesWindowY);
        set
        {
            EnemiesWindowX = value.X;
            EnemiesWindowY = value.Y;
        }
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectScreenshotsPathCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenScreenshotsPathCommand { get; }

    public ReactiveCommand<Unit, Unit> ConfigureDisplayCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelConfigurationCommand { get; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenAuthWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenLoadingScreenWindowCommand { get; }

    public MainViewModel(
        AppSettings settings,
        IAuthorizationService authorizationService,
        ILogger<MainViewModel> logger)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _logger = logger;

        _screenshotsPath = settings.ReplaysPath;
        _hideStatisticsHotkey = settings.HideStatisticsHotkey;
        _hideStatisticsCtrl = settings.HideStatisticsCtrl;
        _hideStatisticsAlt = settings.HideStatisticsAlt;
        _hideStatisticsShift = settings.HideStatisticsShift;
        _alliesWindowX = settings.AlliesWindowX;
        _alliesWindowY = settings.AlliesWindowY;
        _enemiesWindowX = settings.EnemiesWindowX;
        _enemiesWindowY = settings.EnemiesWindowY;
        _minimizeToTrayOnClose = settings.MinimizeToTrayOnClose;

        _originalAlliesWindowX = settings.AlliesWindowX;
        _originalAlliesWindowY = settings.AlliesWindowY;
        _originalEnemiesWindowX = settings.EnemiesWindowX;
        _originalEnemiesWindowY = settings.EnemiesWindowY;

        var uiScheduler = RxApp.MainThreadScheduler;

        SaveCommand = ReactiveCommand.Create(SaveSettings, outputScheduler: uiScheduler);
        SelectScreenshotsPathCommand = ReactiveCommand.CreateFromTask(SelectScreenshotsPath, outputScheduler: uiScheduler);
        OpenScreenshotsPathCommand = ReactiveCommand.Create(OpenScreenshotsPath, outputScheduler: uiScheduler);
        ConfigureDisplayCommand = ReactiveCommand.CreateFromTask(ConfigureDisplay, outputScheduler: uiScheduler);
        CancelConfigurationCommand = ReactiveCommand.Create(CancelConfiguration, outputScheduler: uiScheduler);
        ExitCommand = ReactiveCommand.Create(Exit, outputScheduler: uiScheduler);
        OpenAuthWindowCommand = ReactiveCommand.Create(OpenAuthWindow, outputScheduler: uiScheduler);
        OpenLoadingScreenWindowCommand = ReactiveCommand.Create(OpenLoadingScreenWindow, outputScheduler: uiScheduler);

        UpdateAuthStatus();
        CheckLoadingScreenStatus();
        SaveSettings();

        this.WhenAnyValue(
                x => x.HideStatisticsHotkey,
                x => x.HideStatisticsCtrl,
                x => x.HideStatisticsAlt,
                x => x.HideStatisticsShift)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HotkeyDisplayText)));
    }

    private void SaveSettings()
    {
        try
        {
            _settings.ReplaysPath = ScreenshotsPath;
            _settings.HideStatisticsHotkey = HideStatisticsHotkey;
            _settings.HideStatisticsCtrl = HideStatisticsCtrl;
            _settings.HideStatisticsAlt = HideStatisticsAlt;
            _settings.HideStatisticsShift = HideStatisticsShift;
            _settings.AlliesWindowX = AlliesWindowX;
            _settings.AlliesWindowY = AlliesWindowY;
            _settings.EnemiesWindowX = EnemiesWindowX;
            _settings.EnemiesWindowY = EnemiesWindowY;
            _settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;

            AppSettings.Save(_settings);

            Dispatcher.UIThread.Post(ApplyWindowPositions);
            App.UpdateGlobalHotkey();
            if (IsDisplayConfigurationMode)
            {
                ExitConfigurationMode();
            }

            _logger.LogInformation("Настройки сохранены и применены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении настроек");
        }
    }

    private async Task SelectScreenshotsPath()
    {
        try
        {
            var mainWindow = App.MainWindow;
            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null)
            {
                _logger.LogWarning("Не удалось получить TopLevel для открытия диалога выбора папки");
                return;
            }

            var folderDialog = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Выберите папку с сохранёнными реплеями",
                    AllowMultiple = false
                });

            if (folderDialog.Count > 0)
            {
                ScreenshotsPath = folderDialog[0].TryGetLocalPath() ?? ScreenshotsPath;
                _logger.LogInformation("Выбран новый путь сохранённх реплеев: {Path}", ScreenshotsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выборе пути для сохранённх реплеев");
        }
    }

    private void OpenScreenshotsPath()
    {
        try
        {
            var pathToOpen = ScreenshotsPath;

            if (!Directory.Exists(pathToOpen))
            {
                Directory.CreateDirectory(pathToOpen);
                _logger.LogInformation("Создана директория для скриншотов: {Path}", pathToOpen);
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = pathToOpen,
                    UseShellExecute = true
                });

            _logger.LogInformation("Открыта директория со скриншотами: {Path}", pathToOpen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при открытии папки скриншотов");
        }
    }

    private void ApplyWindowPositions()
    {
        try
        {
            if (App.AlliesWindow != null)
                App.AlliesWindow.Position = AlliesWindowPosition;

            if (App.EnemiesWindow == null)
                return;

            var leftX = EnemiesWindowPosition.X - (int)App.EnemiesWindow.Bounds.Width;
            var topY = EnemiesWindowPosition.Y;

            App.EnemiesWindow.Position = new PixelPoint(leftX, topY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при применении настроек к окнам");
        }
    }

    public void UpdateWindowPosition(string windowType, PixelPoint position)
    {
        try
        {
            switch (windowType)
            {
                case "Allies":
                    AlliesWindowX = position.X;
                    AlliesWindowY = position.Y;
                    AlliesWindowPosition = position;
                    break;
                case "Enemies":
                    EnemiesWindowX = position.X;
                    EnemiesWindowY = position.Y;
                    EnemiesWindowPosition = position;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении позиции окна {WindowType}", windowType);
        }
    }

    private async Task ConfigureDisplay()
    {
        try
        {
            _originalAlliesWindowX = AlliesWindowX;
            _originalAlliesWindowY = AlliesWindowY;
            _originalEnemiesWindowX = EnemiesWindowX;
            _originalEnemiesWindowY = EnemiesWindowY;

            IsDisplayConfigurationMode = true;
            IsWindowsVisible = true;
            IsBattleWindowsVisible = true;

            if (App.AlliesWindow != null && App.EnemiesWindow != null)
                if (App.AlliesWindow.DataContext is BattleStatisticsViewModel alliesViewModel &&
                    App.EnemiesWindow.DataContext is BattleStatisticsViewModel enemiesViewModel)
                {
                    var hasAlliesData = alliesViewModel.Allies.Count > 0;
                    var hasEnemiesData = enemiesViewModel.Enemies.Count > 0;

                    if (!hasAlliesData && !hasEnemiesData)
                    {
                        await alliesViewModel.ShowExamples();
                        await enemiesViewModel.ShowExamples();
                    }
                    else
                    {
                        ConfigurationModeWithAlreadyData = true;
                    }
                }

            ApplyWindowPositions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при активации режима настройки отображения");
        }
    }

    public void ToggleWindowsVisibility()
    {
        try
        {
            if (IsDisplayConfigurationMode)
            {
                var oldState = IsWindowsVisible;
                IsWindowsVisible = !IsWindowsVisible;

                _logger.LogInformation("Переключение видимости в режиме настройки: {OldState} -> {IsWindowsVisible}", oldState, IsWindowsVisible);

                if (IsWindowsVisible)
                {
                    App.AlliesWindow?.Show();
                    App.EnemiesWindow?.Show();
                }
                else
                {
                    App.AlliesWindow?.Hide();
                    App.EnemiesWindow?.Hide();
                }
            }
            else
            {
                var oldState = IsBattleWindowsVisible;
                IsBattleWindowsVisible = !IsBattleWindowsVisible;

                _logger.LogInformation("Переключение видимости в бою: {OldState} -> {IsWindowsVisible}", oldState, IsBattleWindowsVisible);

                if (!IsBattleWindowsVisible)
                {
                    App.AlliesWindow?.Hide();
                    App.EnemiesWindow?.Hide();
                }
                else
                {
                    if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel battleViewModel)
                    {
                        battleViewModel.UpdateWindowVisibility();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при переключении видимости окон");
        }
    }

    private void ExitConfigurationMode()
    {
        try
        {
            IsDisplayConfigurationMode = false;

            if (ConfigurationModeWithAlreadyData ||
                App.AlliesWindow?.DataContext is not BattleStatisticsViewModel alliesViewModel ||
                App.EnemiesWindow?.DataContext is not BattleStatisticsViewModel enemiesViewModel)
            {
                ConfigurationModeWithAlreadyData = false;

                return;
            }

            ConfigurationModeWithAlreadyData = false;

            alliesViewModel.EraseExamples();
            enemiesViewModel.EraseExamples();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении режима настройки отображения");
        }
    }

    private void OpenAuthWindow()
    {
        try
        {
            if (_currentAuthWindow is { IsVisible: false })
                _currentAuthWindow = null;

            if (_currentAuthWindow != null)
            {
                _currentAuthWindow.Activate();
                return;
            }

            _currentAuthWindow = new Windows_AuthorizationWindow(App.ServiceProvider.GetRequiredService<AuthorizationViewModel>());
            _currentAuthWindow.Closed += (_, _) =>
            {
                UpdateAuthStatus();
                _currentAuthWindow = null;
            };

            _currentAuthWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при открытии окна авторизации");
        }
    }

    private void UpdateAuthStatus()
    {
        this.RaisePropertyChanged(nameof(IsApiKeyExists));
        this.RaisePropertyChanged(nameof(AuthDisplayText));
    }

    private void CancelConfiguration()
    {
        try
        {
            AlliesWindowX = _originalAlliesWindowX;
            AlliesWindowY = _originalAlliesWindowY;
            EnemiesWindowX = _originalEnemiesWindowX;
            EnemiesWindowY = _originalEnemiesWindowY;

            ExitConfigurationMode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отмене настройки отображения");
        }
    }

    private static void Exit()
    {
        App.MainWindow?.Close();
    }

    private void OpenLoadingScreenWindow()
    {
        try
        {
            if (_currentLoadingScreenWindow is { IsVisible: false })
                _currentLoadingScreenWindow = null;

            if (_currentLoadingScreenWindow != null)
            {
                _currentLoadingScreenWindow.Activate();
                return;
            }

            if (App.MainWindow == null)
            {
                _logger.LogWarning("Главное окно не найдено");
                return;
            }

            _currentLoadingScreenWindow = new LoadingScreenWindow();
            _currentLoadingScreenWindow.DataContext = new LoadingScreenViewModel(_currentLoadingScreenWindow, CheckLoadingScreenStatus);

            _currentLoadingScreenWindow.Closed += (_, _) =>
            {
                CheckLoadingScreenStatus();
                _currentLoadingScreenWindow = null;
            };

            _currentLoadingScreenWindow.Show(App.MainWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при открытии окна настройки экрана загрузки");
        }
    }

    private void CheckLoadingScreenStatus()
    {
        try
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(AppSettings.SettingsPath)!,
                "Backup Loading Screen");

            IsLoadingScreenReplaced = Directory.Exists(backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке статуса замены экрана загрузки");
            IsLoadingScreenReplaced = false;
        }
    }
}
