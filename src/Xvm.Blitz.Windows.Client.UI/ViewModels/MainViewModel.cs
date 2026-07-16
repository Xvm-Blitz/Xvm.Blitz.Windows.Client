using System.Diagnostics;
using System.Reactive;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.Windows;
using Windows_AuthorizationWindow = Xvm.Blitz.Windows.Client.UI.Windows.AuthorizationWindow;
using LoadingScreenWindow = Xvm.Blitz.Windows.Client.UI.Windows.LoadingScreenWindow;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class MainViewModel : ReactiveObject, IDisposable
{
    private static Windows_AuthorizationWindow? _currentAuthWindow;
    private static LoadingScreenWindow? _currentLoadingScreenWindow;
    private static TutorialWindow? _currentTutorialWindow;

    private readonly IAppUpdateService _appUpdateService;

    private readonly IAuthorizationService _authorizationService;

    private readonly ILogger<MainViewModel> _logger;

    private readonly Timer _updateCheckTimer;

    private readonly AppSettings _settings;

    private readonly string _currentVersion;

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

    private string _replaysPath;

    private bool _isLoadingScreenReplaced;

    private bool _isLoadingScreenWarningVisible;

    private bool _isUpdateAvailable;

    private bool _isUpToDate;

    private string? _latestVersion;

    private string? _updateDownloadUrl;

    public string ReplaysPath
    {
        get => _replaysPath;
        set => this.RaiseAndSetIfChanged(ref _replaysPath, value);
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
        set
        {
            this.RaiseAndSetIfChanged(ref _isLoadingScreenReplaced, value);
            if (value)
                IsLoadingScreenWarningVisible = false;
        }
    }

    public bool IsLoadingScreenWarningVisible
    {
        get => _isLoadingScreenWarningVisible;
        set => this.RaiseAndSetIfChanged(ref _isLoadingScreenWarningVisible, value);
    }

    public string CurrentVersionText => $"Текущая версия: {_currentVersion}";

    public string LatestVersionText =>
        string.IsNullOrWhiteSpace(_latestVersion)
            ? string.Empty
            : $"Последняя версия: {_latestVersion}";

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
    }

    public bool IsUpToDate
    {
        get => _isUpToDate;
        private set => this.RaiseAndSetIfChanged(ref _isUpToDate, value);
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

    public ReactiveCommand<Unit, Unit> SelectReplaysPathCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenReplaysPathCommand { get; }

    public ReactiveCommand<Unit, Unit> ConfigureDisplayCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelConfigurationCommand { get; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenAuthWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenLoadingScreenWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    public ReactiveCommand<Unit, Unit> DownloadUpdateCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenTutorialCommand { get; }

    public MainViewModel(
        AppSettings settings,
        IAuthorizationService authorizationService,
        IAppUpdateService appUpdateService,
        ILogger<MainViewModel> logger)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _appUpdateService = appUpdateService;
        _logger = logger;
        _currentVersion = ResolveCurrentVersion();

        _replaysPath = settings.ReplaysPath;
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
        SelectReplaysPathCommand = ReactiveCommand.CreateFromTask(SelectReplaysPath, outputScheduler: uiScheduler);
        OpenReplaysPathCommand = ReactiveCommand.Create(OpenReplaysPath, outputScheduler: uiScheduler);
        ConfigureDisplayCommand = ReactiveCommand.CreateFromTask(ConfigureDisplay, outputScheduler: uiScheduler);
        CancelConfigurationCommand = ReactiveCommand.Create(CancelConfiguration, outputScheduler: uiScheduler);
        ExitCommand = ReactiveCommand.Create(Exit, outputScheduler: uiScheduler);
        OpenAuthWindowCommand = ReactiveCommand.Create(OpenAuthWindow, outputScheduler: uiScheduler);
        OpenLoadingScreenWindowCommand = ReactiveCommand.Create(OpenLoadingScreenWindow, outputScheduler: uiScheduler);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync, outputScheduler: uiScheduler);
        DownloadUpdateCommand = ReactiveCommand.Create(DownloadUpdate, outputScheduler: uiScheduler);
        OpenTutorialCommand = ReactiveCommand.Create(OpenTutorial, outputScheduler: uiScheduler);

        _updateCheckTimer = new Timer(
            _ => Dispatcher.UIThread.InvokeAsync(CheckForUpdatesAsync),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(10));

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

    public void Dispose()
    {
        _updateCheckTimer.Dispose();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.ReplaysPath = ReplaysPath;
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

            _logger.LogInformation("Settings saved and applied");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
        }
    }

    private async Task SelectReplaysPath()
    {
        try
        {
            var mainWindow = App.MainWindow;
            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null)
            {
                _logger.LogWarning("Failed to get TopLevel to open folder picker dialog");
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
                ReplaysPath = folderDialog[0].TryGetLocalPath() ?? ReplaysPath;
                _logger.LogInformation("Selected new saved replays path: {Path}", ReplaysPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting saved replays path");
        }
    }

    private void OpenReplaysPath()
    {
        try
        {
            var pathToOpen = ReplaysPath;

            if (!Directory.Exists(pathToOpen))
            {
                Directory.CreateDirectory(pathToOpen);
                _logger.LogInformation("Created replays directory: {Path}", pathToOpen);
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = pathToOpen,
                    UseShellExecute = true
                });

            _logger.LogInformation("Opened replays directory: {Path}", pathToOpen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening replays folder");
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
            _logger.LogError(ex, "Error applying settings to windows");
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
            _logger.LogError(ex, "Error updating window position {WindowType}", windowType);
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
            _logger.LogError(ex, "Error activating display setup mode");
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

                _logger.LogInformation("Toggling visibility in display setup mode: {OldState} -> {IsWindowsVisible}", oldState, IsWindowsVisible);

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

                _logger.LogInformation("Toggling visibility in battle: {OldState} -> {IsWindowsVisible}", oldState, IsBattleWindowsVisible);

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
            _logger.LogError(ex, "Error toggling window visibility");
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
            _logger.LogError(ex, "Error exiting display setup mode");
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
            _logger.LogError(ex, "Error opening authorization window");
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
            _logger.LogError(ex, "Error canceling display setup");
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
                _logger.LogWarning("Main window not found");
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
            _logger.LogError(ex, "Error opening loading screen setup window");
        }
    }

    public void OpenTutorial()
    {
        try
        {
            if (_currentTutorialWindow is { IsVisible: false })
                _currentTutorialWindow = null;

            if (_currentTutorialWindow != null)
            {
                _currentTutorialWindow.Activate();
                return;
            }

            if (App.MainWindow == null)
            {
                _logger.LogWarning("Main window not found");
                return;
            }

            var tutorialViewModel = new TutorialViewModel(
                _settings,
                () => Dispatcher.UIThread.Post(() => _currentTutorialWindow?.Close()));

            _currentTutorialWindow = new TutorialWindow(tutorialViewModel);
            _currentTutorialWindow.Closed += (_, _) =>
            {
                tutorialViewModel.MarkAsSeen();
                _currentTutorialWindow = null;
            };
            _currentTutorialWindow.Show(App.MainWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening tutorial window");
        }
    }

    public bool ShouldShowTutorialOnStartup => !_settings.HasSeenTutorial;

    private void CheckLoadingScreenStatus()
    {
        try
        {
            IsLoadingScreenReplaced = LoadingScreenPatch.IsReplaced;
            IsLoadingScreenWarningVisible = !IsLoadingScreenReplaced;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking loading screen replacement status");
            IsLoadingScreenReplaced = false;
            IsLoadingScreenWarningVisible = true;
        }
    }

    public void NotifyLoadingScreenRequired()
    {
        IsLoadingScreenWarningVisible = true;
        CheckLoadingScreenStatus();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateInfo = await _appUpdateService.GetLatestVersion(_currentVersion, ClientPlatform.Windows);
            if (updateInfo is null || string.IsNullOrWhiteSpace(updateInfo.Version))
                return;

            var hasUpdate = SemVerComparer.IsLessThan(_currentVersion, updateInfo.Version);
            _latestVersion = updateInfo.Version;
            _updateDownloadUrl = updateInfo.DownloadUrl;
            IsUpdateAvailable = hasUpdate;
            IsUpToDate = !hasUpdate;
            this.RaisePropertyChanged(nameof(LatestVersionText));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error checking for application updates");
        }
    }

    private void DownloadUpdate()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_updateDownloadUrl))
                return;

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = _updateDownloadUrl,
                    UseShellExecute = true
                });

            _logger.LogInformation("Opened update download url: {DownloadUrl}", _updateDownloadUrl);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error opening update download url");
        }
    }

    private static string ResolveCurrentVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Split('+')[0];

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
