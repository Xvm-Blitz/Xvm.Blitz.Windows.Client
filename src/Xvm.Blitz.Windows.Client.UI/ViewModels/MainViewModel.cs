using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;
using Xvm.Blitz.Windows.Client.Core.Services;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.ViewModels.Models;
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

    private readonly IBattleSessionCredentialsService _battleSessionCredentialsService;

    private readonly IBattleSessionRuntimeService _battleSessionRuntimeService;

    private readonly ISessionsClient _sessionsClient;

    private readonly IUsageService _usageService;

    private readonly ILogger<MainViewModel> _logger;

    private readonly Timer _updateCheckTimer;

    private readonly AppSettings _settings;

    private readonly string _currentVersion;

    private int _alliesWindowX;

    private int _alliesWindowY;

    private int _enemiesWindowX;

    private int _enemiesWindowY;

    private int _sessionSummaryOverlayX;

    private int _sessionSummaryOverlayY;

    private bool _isSessionSummaryOverlayVisible;

    private double _sessionSummaryOverlayScaleX;

    private double _sessionSummaryOverlayScaleY;

    private bool _isDisplayConfigurationMode;

    private bool _isWindowsVisible = true;

    private int _originalAlliesWindowX;

    private int _originalAlliesWindowY;

    private int _originalEnemiesWindowX;

    private int _originalEnemiesWindowY;

    private int _originalSessionSummaryOverlayX;

    private int _originalSessionSummaryOverlayY;

    private double _originalSessionSummaryOverlayScaleX;

    private double _originalSessionSummaryOverlayScaleY;

    private bool _wasSessionSummaryOverlayVisibleBeforeConfiguration;

    private bool _sessionSummaryOverlayExampleApplied;

    private bool _configurationPreviewShown;

    private string _sessionOverlayBattlesText = "—";

    private string _sessionOverlayWinRateText = "—";

    private string _sessionOverlayDamageText = "—";

    private bool _isBattleWindowsVisible = true;

    private bool _minimizeToTrayOnClose;

    private string _replaysPath;

    private bool _isLoadingScreenReplaced;

    private bool _isLoadingScreenWarningVisible;

    private bool _isUpdateAvailable;

    private bool _isUpToDate;

    private string? _latestVersion;

    private string? _updateDownloadUrl;

    private string _sessionNickname = string.Empty;

    private string _sessionSecretKey = string.Empty;

    private SessionListItem? _selectedSession;

    private bool _isSessionBusy;

    private string? _sessionStatusMessage;

    private bool _isSessionStatusError;

    private int _sessionHistoryPage = 1;

    private int _sessionHistoryTotalCount;

    private bool _isSessionBattlesLoading;

    private bool _isSessionSecretKeyCopiedHighlight;

    private Timer? _sessionSecretKeyHighlightTimer;

    private Timer? _sessionStatusCountdownTimer;

    private DateTimeOffset? _sessionStatusRetryAfter;

    private bool _sessionStatusIsSessionCreateRateLimit;

    private int _sessionBattlesPage = 1;

    private int _sessionBattlesTotalCount;

    private readonly List<SessionBattleListItem> _allSessionBattles = [];

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

    public int SessionSummaryOverlayX
    {
        get => _sessionSummaryOverlayX;
        set => this.RaiseAndSetIfChanged(ref _sessionSummaryOverlayX, value);
    }

    public int SessionSummaryOverlayY
    {
        get => _sessionSummaryOverlayY;
        set => this.RaiseAndSetIfChanged(ref _sessionSummaryOverlayY, value);
    }

    public PixelPoint SessionSummaryOverlayPosition
    {
        get => new(SessionSummaryOverlayX, SessionSummaryOverlayY);
        set
        {
            SessionSummaryOverlayX = value.X;
            SessionSummaryOverlayY = value.Y;
        }
    }

    public bool IsSessionSummaryOverlayVisible
    {
        get => _isSessionSummaryOverlayVisible;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSessionSummaryOverlayVisible, value);
            this.RaisePropertyChanged(nameof(SessionSummaryOverlayButtonText));
            this.RaisePropertyChanged(nameof(IsSessionSummaryOverlayButtonActive));
        }
    }

    public string SessionSummaryOverlayButtonText =>
        IsSessionSummaryOverlayVisible ? "Суммаризация: вкл" : "Суммаризация: выкл";

    public bool IsSessionSummaryOverlayButtonActive => IsSessionSummaryOverlayVisible;

    public string SessionOverlayBattlesText
    {
        get => _sessionOverlayBattlesText;
        private set => this.RaiseAndSetIfChanged(ref _sessionOverlayBattlesText, value);
    }

    public string SessionOverlayWinRateText
    {
        get => _sessionOverlayWinRateText;
        private set => this.RaiseAndSetIfChanged(ref _sessionOverlayWinRateText, value);
    }

    public string SessionOverlayDamageText
    {
        get => _sessionOverlayDamageText;
        private set => this.RaiseAndSetIfChanged(ref _sessionOverlayDamageText, value);
    }

    public bool IsBattleWindowsVisible
    {
        get => _isBattleWindowsVisible;
        set => this.RaiseAndSetIfChanged(ref _isBattleWindowsVisible, value);
    }

    public bool IsDisplayConfigurationMode
    {
        get => _isDisplayConfigurationMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, value);
        }
    }

    public bool IsWindowsVisible
    {
        get => _isWindowsVisible;
        set => this.RaiseAndSetIfChanged(ref _isWindowsVisible, value);
    }

    public bool ConfigurationModeWithAlreadyData { get; set; }

    public double SessionSummaryOverlayScaleX => _sessionSummaryOverlayScaleX;

    public double SessionSummaryOverlayScaleY => _sessionSummaryOverlayScaleY;

    public double SessionSummaryOverlayFontSize =>
        OverlayPanelSizing.SessionOverlayFontSize(_sessionSummaryOverlayScaleY);

    public Thickness SessionSummaryOverlayPadding
    {
        get
        {
            var (horizontal, vertical) = OverlayPanelSizing.SessionOverlayPadding(
                _sessionSummaryOverlayScaleX,
                _sessionSummaryOverlayScaleY);
            return new Thickness(horizontal, vertical);
        }
    }

    public double SessionSummaryOverlaySpacing =>
        OverlayPanelSizing.SessionOverlaySpacing(_sessionSummaryOverlayScaleX, _sessionSummaryOverlayScaleY);

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

    public ReactiveCommand<Unit, Unit> StartSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> RestoreSessionsCommand { get; }

    public ReactiveCommand<Unit, Unit> EndSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> GenerateSessionSecretKeyCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousSessionHistoryPageCommand { get; }

    public ReactiveCommand<Unit, Unit> NextSessionHistoryPageCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousSessionBattlesPageCommand { get; }

    public ReactiveCommand<Unit, Unit> NextSessionBattlesPageCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshSessionBattlesCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleSessionSummaryOverlayCommand { get; }

    public ObservableCollection<SessionListItem> AvailableSessions { get; } = [];

    public ObservableCollection<SessionBattleListItem> SessionBattles { get; } = [];

    public string SessionNickname
    {
        get => _sessionNickname;
        set => this.RaiseAndSetIfChanged(ref _sessionNickname, value);
    }

    public string SessionSecretKey
    {
        get => _sessionSecretKey;
        set => this.RaiseAndSetIfChanged(ref _sessionSecretKey, value);
    }

    public bool IsSessionSecretKeyCopiedHighlight
    {
        get => _isSessionSecretKeyCopiedHighlight;
        private set => this.RaiseAndSetIfChanged(ref _isSessionSecretKeyCopiedHighlight, value);
    }

    public SessionListItem? SelectedSession
    {
        get => _selectedSession;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSession, value);
            this.RaisePropertyChanged(nameof(HasSelectedSession));
            this.RaisePropertyChanged(nameof(CanEndSelectedSession));
            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
            PersistSelectedSession();
            _ = LoadSessionBattlesAsync();
            _ = UpdateActiveSessionConnectionAsync();
        }
    }

    public bool HasSelectedSession => SelectedSession is not null;

    public bool CanEndSelectedSession => !IsSessionBusy && SelectedSession?.IsActive == true;

    public bool IsSessionBattlesLoading
    {
        get => _isSessionBattlesLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSessionBattlesLoading, value);
            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
        }
    }

    public bool HasNoSessionBattles =>
        HasSelectedSession && !IsSessionBattlesLoading && SessionBattlesTotalCount == 0;

    public string SessionBattlesHeader =>
        SelectedSession is null ? "Бои сессии" : $"Бои сессии ({SessionBattlesTotalCount})";

    public int SessionBattlesPage
    {
        get => _sessionBattlesPage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _sessionBattlesPage, value);
            RaiseSessionBattlesPagingChanged();
        }
    }

    public int SessionBattlesTotalCount
    {
        get => _sessionBattlesTotalCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _sessionBattlesTotalCount, value);
            RaiseSessionBattlesPagingChanged();
        }
    }

    public int SessionBattlesTotalPages =>
        Math.Max(1, (int)Math.Ceiling(SessionBattlesTotalCount / (double)SessionBattlesPageSize));

    public string SessionBattlesPageText => $"Стр. {SessionBattlesPage} / {SessionBattlesTotalPages}";

    public bool HasPreviousSessionBattlesPage => SessionBattlesPage > 1;

    public bool HasNextSessionBattlesPage => SessionBattlesPage < SessionBattlesTotalPages;

    public bool ShowSessionBattlesPagination => HasSelectedSession && SessionBattlesTotalCount > 0;

    public bool HasSessionBattlesSummary { get; private set; }

    public string SessionBattlesTotalSummary { get; private set; } = string.Empty;

    public string SessionBattlesWinRateSummary { get; private set; } = string.Empty;

    public string SessionBattlesAverageDamageSummary { get; private set; } = string.Empty;

    public string SessionBattlesAverageFragsSummary { get; private set; } = string.Empty;

    public const string SessionStatisticsDisclaimerText =
        "Точность расширенных расчётов не гарантируется и может не совпадать с реальностью.";

    public bool ShowSessionStatisticsDisclaimer =>
        HasSelectedSession && (HasSessionBattlesSummary || SessionBattlesTotalCount > 0);

    public bool IsSessionBusy
    {
        get => _isSessionBusy;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSessionBusy, value);
            this.RaisePropertyChanged(nameof(CanEndSelectedSession));
        }
    }

    public string? SessionStatusMessage
    {
        get => _sessionStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _sessionStatusMessage, value);
    }

    public bool IsSessionStatusError
    {
        get => _isSessionStatusError;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSessionStatusError, value);
            this.RaisePropertyChanged(nameof(HasSessionStatusError));
            this.RaisePropertyChanged(nameof(HasSessionStatusSuccess));
        }
    }

    public bool HasSessionStatusError => IsSessionStatusError && !string.IsNullOrWhiteSpace(SessionStatusMessage);

    public bool HasSessionStatusSuccess => !IsSessionStatusError && !string.IsNullOrWhiteSpace(SessionStatusMessage);

    public int SessionHistoryPage
    {
        get => _sessionHistoryPage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _sessionHistoryPage, value);
            RaiseSessionHistoryPagingChanged();
        }
    }

    public int SessionHistoryTotalCount
    {
        get => _sessionHistoryTotalCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _sessionHistoryTotalCount, value);
            RaiseSessionHistoryPagingChanged();
        }
    }

    public int SessionHistoryTotalPages =>
        Math.Max(1, (int)Math.Ceiling(SessionHistoryTotalCount / (double)SessionHistoryPageSize));

    public string SessionHistoryPageText => $"Стр. {SessionHistoryPage} / {SessionHistoryTotalPages}";

    public bool HasPreviousSessionHistoryPage => SessionHistoryPage > 1;

    public bool HasNextSessionHistoryPage => SessionHistoryPage < SessionHistoryTotalPages;

    private const int SessionHistoryPageSize = 10;

    private const int SessionBattlesPageSize = 10;

    public MainViewModel(
        AppSettings settings,
        IAuthorizationService authorizationService,
        IAppUpdateService appUpdateService,
        ISessionsClient sessionsClient,
        IUsageService usageService,
        IBattleSessionCredentialsService battleSessionCredentialsService,
        IBattleSessionRuntimeService battleSessionRuntimeService,
        ILogger<MainViewModel> logger)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _appUpdateService = appUpdateService;
        _sessionsClient = sessionsClient;
        _usageService = usageService;
        _battleSessionCredentialsService = battleSessionCredentialsService;
        _battleSessionRuntimeService = battleSessionRuntimeService;
        _logger = logger;
        _currentVersion = ResolveCurrentVersion();

        _replaysPath = settings.ReplaysPath;
        _alliesWindowX = settings.AlliesWindowX;
        _alliesWindowY = settings.AlliesWindowY;
        _enemiesWindowX = settings.EnemiesWindowX;
        _enemiesWindowY = settings.EnemiesWindowY;
        _sessionSummaryOverlayX = settings.SessionSummaryOverlayX;
        _sessionSummaryOverlayY = settings.SessionSummaryOverlayY;
        _isSessionSummaryOverlayVisible = settings.SessionSummaryOverlayVisible;
        _sessionSummaryOverlayScaleX = OverlayPanelSizing.CoerceScaleX(settings.SessionSummaryOverlayScaleX);
        _sessionSummaryOverlayScaleY = OverlayPanelSizing.CoerceScaleY(settings.SessionSummaryOverlayScaleY);
        _minimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        _sessionNickname = settings.SessionNickname;

        _originalAlliesWindowX = settings.AlliesWindowX;
        _originalAlliesWindowY = settings.AlliesWindowY;
        _originalEnemiesWindowX = settings.EnemiesWindowX;
        _originalEnemiesWindowY = settings.EnemiesWindowY;
        _originalSessionSummaryOverlayX = settings.SessionSummaryOverlayX;
        _originalSessionSummaryOverlayY = settings.SessionSummaryOverlayY;
        _originalSessionSummaryOverlayScaleX = _sessionSummaryOverlayScaleX;
        _originalSessionSummaryOverlayScaleY = _sessionSummaryOverlayScaleY;

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
        StartSessionCommand = ReactiveCommand.CreateFromTask(StartSessionAsync, outputScheduler: uiScheduler);
        RestoreSessionsCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(1),
            outputScheduler: uiScheduler);
        EndSessionCommand = ReactiveCommand.CreateFromTask(EndSessionAsync, outputScheduler: uiScheduler);
        GenerateSessionSecretKeyCommand = ReactiveCommand.CreateFromTask(GenerateSessionSecretKeyAsync, outputScheduler: uiScheduler);
        PreviousSessionHistoryPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(SessionHistoryPage - 1),
            this.WhenAnyValue(viewModel => viewModel.HasPreviousSessionHistoryPage),
            uiScheduler);
        NextSessionHistoryPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(SessionHistoryPage + 1),
            this.WhenAnyValue(viewModel => viewModel.HasNextSessionHistoryPage),
            uiScheduler);
        PreviousSessionBattlesPageCommand = ReactiveCommand.Create(
            () => GoToSessionBattlesPage(SessionBattlesPage - 1),
            this.WhenAnyValue(viewModel => viewModel.HasPreviousSessionBattlesPage),
            uiScheduler);
        NextSessionBattlesPageCommand = ReactiveCommand.Create(
            () => GoToSessionBattlesPage(SessionBattlesPage + 1),
            this.WhenAnyValue(viewModel => viewModel.HasNextSessionBattlesPage),
            uiScheduler);
        RefreshSessionBattlesCommand = ReactiveCommand.CreateFromTask(
            LoadSessionBattlesAsync,
            this.WhenAnyValue(viewModel => viewModel.HasSelectedSession),
            uiScheduler);
        ToggleSessionSummaryOverlayCommand = ReactiveCommand.Create(
            ToggleSessionSummaryOverlay,
            this.WhenAnyValue(viewModel => viewModel.HasSelectedSession),
            uiScheduler);

        _updateCheckTimer = new Timer(
            _ => Dispatcher.UIThread.InvokeAsync(CheckForUpdatesAsync),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(10));

        _battleSessionRuntimeService.BattleStarted += OnSessionBattleStarted;
        _battleSessionRuntimeService.BattleCompleted += OnSessionBattleCompleted;
        _battleSessionRuntimeService.SessionEnded += OnSessionEnded;

        UpdateAuthStatus();
        CheckLoadingScreenStatus();
        SaveSettings();
        _ = InitializeSessionsAsync();
    }

    public void HideSessionSummaryOverlay()
    {
        if (!IsSessionSummaryOverlayVisible)
            return;

        IsSessionSummaryOverlayVisible = false;
        _settings.SessionSummaryOverlayVisible = false;
        AppSettings.Save(_settings);
        App.HideSessionSummaryOverlay();
    }

    private void ToggleSessionSummaryOverlay()
    {
        if (IsSessionSummaryOverlayVisible)
        {
            HideSessionSummaryOverlay();
            return;
        }

        IsSessionSummaryOverlayVisible = true;
        _settings.SessionSummaryOverlayVisible = true;
        AppSettings.Save(_settings);
        App.ShowSessionSummaryOverlay();
    }

    public void ApplySessionSummaryOverlayVisibility()
    {
        if (!IsSessionSummaryOverlayVisible)
        {
            App.HideSessionSummaryOverlay();
            return;
        }

        App.ShowSessionSummaryOverlay();
    }

    public void Dispose()
    {
        _battleSessionRuntimeService.BattleStarted -= OnSessionBattleStarted;
        _battleSessionRuntimeService.BattleCompleted -= OnSessionBattleCompleted;
        _battleSessionRuntimeService.SessionEnded -= OnSessionEnded;
        _updateCheckTimer.Dispose();
        _sessionSecretKeyHighlightTimer?.Dispose();
        StopSessionStatusCountdown();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.ReplaysPath = ReplaysPath;
            _settings.AlliesWindowX = AlliesWindowX;
            _settings.AlliesWindowY = AlliesWindowY;
            _settings.EnemiesWindowX = EnemiesWindowX;
            _settings.EnemiesWindowY = EnemiesWindowY;
            _settings.SessionSummaryOverlayX = SessionSummaryOverlayX;
            _settings.SessionSummaryOverlayY = SessionSummaryOverlayY;
            _settings.SessionSummaryOverlayScaleX = SessionSummaryOverlayScaleX;
            _settings.SessionSummaryOverlayScaleY = SessionSummaryOverlayScaleY;

            if (_sessionSummaryOverlayExampleApplied && !_wasSessionSummaryOverlayVisibleBeforeConfiguration)
            {
                IsSessionSummaryOverlayVisible = false;
                _settings.SessionSummaryOverlayVisible = false;
            }
            else
            {
                _settings.SessionSummaryOverlayVisible = IsSessionSummaryOverlayVisible;
            }

            _settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
            _settings.SessionNickname = SessionNickname.Trim();
            _settings.SelectedSessionId = SelectedSession?.Id;

            if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel battleStatisticsViewModel)
                battleStatisticsViewModel.PersistPanelScale();

            AppSettings.Save(_settings);

            Dispatcher.UIThread.Post(ApplyWindowPositions);
            if (IsDisplayConfigurationMode)
                ExitConfigurationMode();

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

            if (App.EnemiesWindow != null)
            {
                var leftX = EnemiesWindowPosition.X - (int)App.EnemiesWindow.Bounds.Width;
                var topY = EnemiesWindowPosition.Y;
                App.EnemiesWindow.Position = new PixelPoint(leftX, topY);
            }

            if (App.SessionSummaryOverlayWindow != null)
                App.SessionSummaryOverlayWindow.Position = SessionSummaryOverlayPosition;
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
                    _settings.AlliesWindowX = position.X;
                    _settings.AlliesWindowY = position.Y;
                    AppSettings.Save(_settings);
                    break;
                case "Enemies":
                    EnemiesWindowX = position.X;
                    EnemiesWindowY = position.Y;
                    EnemiesWindowPosition = position;
                    _settings.EnemiesWindowX = position.X;
                    _settings.EnemiesWindowY = position.Y;
                    AppSettings.Save(_settings);
                    break;
                case "SessionSummary":
                    SessionSummaryOverlayX = position.X;
                    SessionSummaryOverlayY = position.Y;
                    SessionSummaryOverlayPosition = position;
                    _settings.SessionSummaryOverlayX = position.X;
                    _settings.SessionSummaryOverlayY = position.Y;
                    AppSettings.Save(_settings);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating window position {WindowType}", windowType);
        }
    }

    public void HidePanels()
    {
        try
        {
            if (IsDisplayConfigurationMode)
            {
                IsWindowsVisible = false;
                App.AlliesWindow?.Hide();
                App.EnemiesWindow?.Hide();
                App.HideSessionSummaryOverlay();
                return;
            }

            _ = App.BattleStatisticsService.EndBattleNotify();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding panels");
        }
    }

    public void SetSessionSummaryOverlayScale(double scaleX, double scaleY)
    {
        _sessionSummaryOverlayScaleX = OverlayPanelSizing.CoerceScaleX(scaleX);
        _sessionSummaryOverlayScaleY = OverlayPanelSizing.CoerceScaleY(scaleY);
        this.RaisePropertyChanged(nameof(SessionSummaryOverlayScaleX));
        this.RaisePropertyChanged(nameof(SessionSummaryOverlayScaleY));
        this.RaisePropertyChanged(nameof(SessionSummaryOverlayFontSize));
        this.RaisePropertyChanged(nameof(SessionSummaryOverlayPadding));
        this.RaisePropertyChanged(nameof(SessionSummaryOverlaySpacing));
    }

    public void PersistSessionSummaryOverlayScale()
    {
        _settings.SessionSummaryOverlayScaleX = SessionSummaryOverlayScaleX;
        _settings.SessionSummaryOverlayScaleY = SessionSummaryOverlayScaleY;
    }

    public void RestoreSessionSummaryOverlayScale(double scaleX, double scaleY) =>
        SetSessionSummaryOverlayScale(scaleX, scaleY);

    private async Task ConfigureDisplay()
    {
        try
        {
            if (IsDisplayConfigurationMode)
                return;

            _originalAlliesWindowX = AlliesWindowX;
            _originalAlliesWindowY = AlliesWindowY;
            _originalEnemiesWindowX = EnemiesWindowX;
            _originalEnemiesWindowY = EnemiesWindowY;
            _originalSessionSummaryOverlayX = SessionSummaryOverlayX;
            _originalSessionSummaryOverlayY = SessionSummaryOverlayY;
            _originalSessionSummaryOverlayScaleX = _sessionSummaryOverlayScaleX;
            _originalSessionSummaryOverlayScaleY = _sessionSummaryOverlayScaleY;

            IsDisplayConfigurationMode = true;
            IsWindowsVisible = true;
            IsBattleWindowsVisible = true;
            _configurationPreviewShown = false;
            ConfigurationModeWithAlreadyData = false;

            if (App.AlliesWindow != null && App.EnemiesWindow != null
                && App.AlliesWindow.DataContext is BattleStatisticsViewModel alliesViewModel
                && App.EnemiesWindow.DataContext is BattleStatisticsViewModel enemiesViewModel)
            {
                alliesViewModel.IsDisplayConfigurationMode = true;
                enemiesViewModel.IsDisplayConfigurationMode = true;

                var hasAlliesData = alliesViewModel.Allies.Count > 0;
                var hasEnemiesData = enemiesViewModel.Enemies.Count > 0;

                if (!hasAlliesData && !hasEnemiesData)
                {
                    await alliesViewModel.ShowExamples();
                    await enemiesViewModel.ShowExamples();
                    _configurationPreviewShown = true;
                }
                else
                {
                    ConfigurationModeWithAlreadyData = true;
                }
            }

            _wasSessionSummaryOverlayVisibleBeforeConfiguration = IsSessionSummaryOverlayVisible;
            _sessionSummaryOverlayExampleApplied = false;

            if (!IsSessionSummaryOverlayVisible)
            {
                ApplySessionOverlayExampleSummary();
                _sessionSummaryOverlayExampleApplied = true;
                IsSessionSummaryOverlayVisible = true;
            }

            App.ShowSessionSummaryOverlay();
            ApplyWindowPositions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating display setup mode");
        }
    }

    private void ExitConfigurationMode()
    {
        try
        {
            IsDisplayConfigurationMode = false;
            IsWindowsVisible = true;

            if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel alliesViewModel)
                alliesViewModel.IsDisplayConfigurationMode = false;

            if (App.EnemiesWindow?.DataContext is BattleStatisticsViewModel enemiesViewModel)
                enemiesViewModel.IsDisplayConfigurationMode = false;

            if (_sessionSummaryOverlayExampleApplied && !_wasSessionSummaryOverlayVisibleBeforeConfiguration)
            {
                ClearSessionOverlaySummary();
                IsSessionSummaryOverlayVisible = false;
                App.HideSessionSummaryOverlay();
            }

            _sessionSummaryOverlayExampleApplied = false;

            if (_configurationPreviewShown &&
                !ConfigurationModeWithAlreadyData &&
                App.AlliesWindow?.DataContext is BattleStatisticsViewModel clearAlliesViewModel &&
                App.EnemiesWindow?.DataContext is BattleStatisticsViewModel clearEnemiesViewModel)
            {
                clearAlliesViewModel.EraseExamples();
                clearEnemiesViewModel.EraseExamples();
            }

            _configurationPreviewShown = false;
            ConfigurationModeWithAlreadyData = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exiting display setup mode");
        }
    }

    private void CancelConfiguration()
    {
        try
        {
            AlliesWindowX = _originalAlliesWindowX;
            AlliesWindowY = _originalAlliesWindowY;
            EnemiesWindowX = _originalEnemiesWindowX;
            EnemiesWindowY = _originalEnemiesWindowY;
            SessionSummaryOverlayX = _originalSessionSummaryOverlayX;
            SessionSummaryOverlayY = _originalSessionSummaryOverlayY;
            RestoreSessionSummaryOverlayScale(
                _originalSessionSummaryOverlayScaleX,
                _originalSessionSummaryOverlayScaleY);

            if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel battleStatisticsViewModel)
                battleStatisticsViewModel.RestorePanelScaleFromSettings();

            if (_sessionSummaryOverlayExampleApplied && !_wasSessionSummaryOverlayVisibleBeforeConfiguration)
            {
                ClearSessionOverlaySummary();
                IsSessionSummaryOverlayVisible = false;
                App.HideSessionSummaryOverlay();
            }
            else if (_wasSessionSummaryOverlayVisibleBeforeConfiguration)
            {
                IsSessionSummaryOverlayVisible = true;
                App.ShowSessionSummaryOverlay();
            }

            _sessionSummaryOverlayExampleApplied = false;
            ExitConfigurationMode();
            ApplyWindowPositions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling display setup");
        }
    }

    private void ApplySessionOverlayExampleSummary()
    {
        SessionOverlayBattlesText = "12 боёв";
        SessionOverlayWinRateText = "58.3%";
        SessionOverlayDamageText = "1840 ур";
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

    private async Task InitializeSessionsAsync()
    {
        try
        {
            SessionSecretKey = await _battleSessionCredentialsService.LoadSecretKey() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(SessionNickname) || string.IsNullOrWhiteSpace(SessionSecretKey))
                return;

            await LoadSessionHistoryAsync(1);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error initializing sessions");
        }
    }

    private async Task GenerateSessionSecretKeyAsync()
    {
        var secretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        SessionSecretKey = secretKey;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            await clipboard.SetTextAsync(secretKey);

        IsSessionSecretKeyCopiedHighlight = true;
        _sessionSecretKeyHighlightTimer?.Dispose();
        _sessionSecretKeyHighlightTimer = new Timer(
            _ => Dispatcher.UIThread.Post(() => IsSessionSecretKeyCopiedHighlight = false),
            null,
            TimeSpan.FromSeconds(3),
            Timeout.InfiniteTimeSpan);

        SetSessionStatus("Секретный ключ сгенерирован и скопирован в буфер обмена", isError: false);
    }

    private async Task StartSessionAsync()
    {
        if (IsSessionBusy)
            return;

        var nickname = SessionNickname.Trim();
        var secretKey = SessionSecretKey.Trim();
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(secretKey))
        {
            SetSessionStatus("Укажите никнейм и секретный ключ", isError: true);
            return;
        }

        IsSessionBusy = true;
        SetSessionStatus("Создание сессии…", isError: false);

        try
        {
            var result = await _sessionsClient.Create(nickname, secretKey);
            if (!result.IsSuccess || result.SessionId is null)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось создать сессию",
                    isError: true,
                    result.RetryAfter,
                    isSessionCreateRateLimit: result.RetryAfter is not null);
                return;
            }

            await PersistSessionCredentialsAsync(nickname, secretKey);
            await LoadSessionHistoryAsync(1, showBusy: false);
            SelectedSession = AvailableSessions.FirstOrDefault(item => item.Id == result.SessionId.Value)
                              ?? AvailableSessions.FirstOrDefault(item => item.IsActive);
            SetSessionStatus("Сессия создана", isError: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error creating session");
            SetSessionStatus(exception.Message, isError: true);
        }
        finally
        {
            IsSessionBusy = false;
        }
    }

    private async Task LoadSessionHistoryAsync(int page, bool showBusy = true)
    {
        if (page < 1)
            return;

        if (showBusy && IsSessionBusy)
            return;

        var nickname = SessionNickname.Trim();
        var secretKey = SessionSecretKey.Trim();
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(secretKey))
        {
            SetSessionStatus("Укажите никнейм и секретный ключ", isError: true);
            return;
        }

        if (showBusy)
        {
            IsSessionBusy = true;
            SetSessionStatus("Загрузка истории сессий…", isError: false);
        }

        try
        {
            var result = await _sessionsClient.Restore(nickname, secretKey, page, SessionHistoryPageSize);
            if (!result.IsSuccess || result.Sessions is null)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось загрузить историю сессий",
                    isError: true,
                    result.RetryAfter);
                return;
            }

            await PersistSessionCredentialsAsync(nickname, secretKey);

            var previouslySelectedId = SelectedSession?.Id ?? _settings.SelectedSessionId;
            AvailableSessions.Clear();
            foreach (var session in result.Sessions)
                AvailableSessions.Add(new SessionListItem(session.Id, session.CreatedAt, session.EndedAt));

            SessionHistoryPage = result.Page;
            SessionHistoryTotalCount = result.TotalCount;

            SelectedSession = previouslySelectedId is { } selectedId
                ? AvailableSessions.FirstOrDefault(item => item.Id == selectedId)
                  ?? AvailableSessions.FirstOrDefault(item => item.IsActive)
                  ?? AvailableSessions.FirstOrDefault()
                : AvailableSessions.FirstOrDefault(item => item.IsActive)
                  ?? AvailableSessions.FirstOrDefault();

            if (showBusy)
            {
                SetSessionStatus(
                    SessionHistoryTotalCount == 0
                        ? "История сессий пуста"
                        : $"Всего сессий: {SessionHistoryTotalCount}",
                    isError: false);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error loading session history");
            SetSessionStatus(exception.Message, isError: true);
        }
        finally
        {
            if (showBusy)
                IsSessionBusy = false;
        }
    }

    private async Task EndSessionAsync()
    {
        if (IsSessionBusy)
            return;

        if (SelectedSession is null || !SelectedSession.IsActive)
        {
            SetSessionStatus("Выберите активную сессию для завершения", isError: true);
            return;
        }

        var secretKey = SessionSecretKey.Trim();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            SetSessionStatus("Укажите секретный ключ", isError: true);
            return;
        }

        IsSessionBusy = true;
        SetSessionStatus("Завершение сессии…", isError: false);

        try
        {
            var sessionId = SelectedSession.Id;
            var result = await _sessionsClient.End(sessionId, secretKey);
            if (!result.IsSuccess)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось завершить сессию",
                    isError: true,
                    result.RetryAfter);
                return;
            }

            await LoadSessionHistoryAsync(SessionHistoryPage, showBusy: false);
            SetSessionStatus("Сессия завершена", isError: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error ending session");
            SetSessionStatus(exception.Message, isError: true);
        }
        finally
        {
            IsSessionBusy = false;
        }
    }

    private async Task PersistSessionCredentialsAsync(string nickname, string secretKey)
    {
        _sessionNickname = nickname;
        this.RaisePropertyChanged(nameof(SessionNickname));
        SessionSecretKey = secretKey;
        _settings.SessionNickname = nickname;
        await _battleSessionCredentialsService.SaveSecretKey(secretKey);
        PersistSelectedSession();
    }

    private void PersistSelectedSession()
    {
        _settings.SessionNickname = SessionNickname.Trim();
        _settings.SelectedSessionId = SelectedSession?.Id;
        AppSettings.Save(_settings);
    }

    private async Task UpdateActiveSessionConnectionAsync()
    {
        try
        {
            if (SelectedSession?.IsActive == true)
            {
                await _battleSessionRuntimeService.SetActiveSessionAsync(SelectedSession.Id, SessionNickname);
                return;
            }

            await _battleSessionRuntimeService.SetActiveSessionAsync(null, null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error connecting session hub");
        }
    }

    private void SetSessionStatus(
        string message,
        bool isError,
        DateTimeOffset? retryAfter = null,
        bool isSessionCreateRateLimit = false)
    {
        StopSessionStatusCountdown();

        SessionStatusMessage = message;
        IsSessionStatusError = isError;
        this.RaisePropertyChanged(nameof(HasSessionStatusError));
        this.RaisePropertyChanged(nameof(HasSessionStatusSuccess));

        if (retryAfter is not { } retryAfterValue || retryAfterValue <= DateTimeOffset.Now)
            return;

        _sessionStatusRetryAfter = retryAfterValue;
        _sessionStatusIsSessionCreateRateLimit = isSessionCreateRateLimit;
        _sessionStatusCountdownTimer = new Timer(
            _ => Dispatcher.UIThread.Post(UpdateSessionStatusCountdown),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void UpdateSessionStatusCountdown()
    {
        if (_sessionStatusRetryAfter is not { } retryAfter)
            return;

        var remainingSeconds = (long)(retryAfter - DateTimeOffset.Now).TotalSeconds;
        if (remainingSeconds <= 0)
        {
            ClearSessionStatus();
            return;
        }

        SessionStatusMessage = _sessionStatusIsSessionCreateRateLimit
            ? HttpErrorMessages.FormatSessionCreateRateLimitMessage(remainingSeconds)
            : HttpErrorMessages.FormatRateLimitCountdown(remainingSeconds);
    }

    private void ClearSessionStatus()
    {
        StopSessionStatusCountdown();
        SessionStatusMessage = null;
        IsSessionStatusError = false;
        this.RaisePropertyChanged(nameof(HasSessionStatusError));
        this.RaisePropertyChanged(nameof(HasSessionStatusSuccess));
    }

    private void StopSessionStatusCountdown()
    {
        _sessionStatusCountdownTimer?.Dispose();
        _sessionStatusCountdownTimer = null;
        _sessionStatusRetryAfter = null;
        _sessionStatusIsSessionCreateRateLimit = false;
    }

    private void RaiseSessionHistoryPagingChanged()
    {
        this.RaisePropertyChanged(nameof(SessionHistoryTotalPages));
        this.RaisePropertyChanged(nameof(SessionHistoryPageText));
        this.RaisePropertyChanged(nameof(HasPreviousSessionHistoryPage));
        this.RaisePropertyChanged(nameof(HasNextSessionHistoryPage));
    }

    private void RaiseSessionBattlesPagingChanged()
    {
        this.RaisePropertyChanged(nameof(SessionBattlesTotalPages));
        this.RaisePropertyChanged(nameof(SessionBattlesPageText));
        this.RaisePropertyChanged(nameof(HasPreviousSessionBattlesPage));
        this.RaisePropertyChanged(nameof(HasNextSessionBattlesPage));
        this.RaisePropertyChanged(nameof(ShowSessionBattlesPagination));
        this.RaisePropertyChanged(nameof(SessionBattlesHeader));
        this.RaisePropertyChanged(nameof(HasNoSessionBattles));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private void ClearSessionBattlesSource()
    {
        _allSessionBattles.Clear();
        SessionBattlesTotalCount = 0;
        SessionBattlesPage = 1;
        SessionBattles.Clear();
    }

    private void SetAllSessionBattles(IEnumerable<SessionBattleListItem> battles)
    {
        _allSessionBattles.Clear();
        _allSessionBattles.AddRange(battles.OrderByDescending(battle => battle.CreatedAt));
        SessionBattlesTotalCount = _allSessionBattles.Count;
        SessionBattlesPage = 1;
        ApplySessionBattlesPage();
    }

    private void GoToSessionBattlesPage(int page)
    {
        SessionBattlesPage = Math.Clamp(page, 1, SessionBattlesTotalPages);
        ApplySessionBattlesPage();
    }

    private void ApplySessionBattlesPage()
    {
        SessionBattles.Clear();

        var skip = (SessionBattlesPage - 1) * SessionBattlesPageSize;
        foreach (var battle in _allSessionBattles.Skip(skip).Take(SessionBattlesPageSize))
            SessionBattles.Add(battle);

        this.RaisePropertyChanged(nameof(HasNoSessionBattles));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private async Task LoadSessionBattlesAsync()
    {
        if (SelectedSession is null)
        {
            ClearSessionBattlesSource();
            ClearSessionBattlesSummary();
            this.RaisePropertyChanged(nameof(SessionBattlesHeader));
            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
            this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
            return;
        }

        if (!_authorizationService.IsApiKeyExists)
        {
            ClearSessionBattlesSource();
            ClearSessionBattlesSummary();
            SetSessionStatus(HttpErrorMessages.DefaultApiKeyMessage, isError: true);
            this.RaisePropertyChanged(nameof(SessionBattlesHeader));
            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
            this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
            return;
        }

        IsSessionBattlesLoading = true;

        try
        {
            GetUsageResponseDto usage;
            try
            {
                usage = await _usageService.Get()
                        ?? throw new InvalidOperationException("Информация об API ключе недоступна");
            }
            catch (Exception exception)
            {
                ClearSessionBattlesSource();
                ClearSessionBattlesSummary();
                SetSessionStatus(exception.Message, isError: true);
                return;
            }

            ClearSessionBattlesSource();

            if (usage.Type is ApiKeyType.Trial)
            {
                var aggregatedResult = await _sessionsClient.GetAggregatedStatistics(SelectedSession.Id);
                if (!aggregatedResult.IsSuccess || aggregatedResult.Statistics is null)
                {
                    SetSessionStatus(
                        aggregatedResult.ErrorMessage ?? "Не удалось загрузить статистику сессии",
                        isError: true);

                    return;
                }

                ApplyAggregatedSummary(aggregatedResult.Statistics);
            }
            else
            {
                var result = await _sessionsClient.GetExtendedStatistics(SelectedSession.Id);
                if (!result.IsSuccess || result.Statistics is null)
                {
                    SetSessionStatus(result.ErrorMessage ?? "Не удалось загрузить бои сессии", isError: true);
                    return;
                }

                SetAllSessionBattles(result.Statistics.Battles.Select(SessionBattleListItem.FromDto));

                UpdateSessionBattlesSummary(result.Statistics.Battles);
            }

            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
            this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error loading session battles");
            SetSessionStatus(exception.Message, isError: true);
        }
        finally
        {
            IsSessionBattlesLoading = false;
        }
    }

    private void ApplyAggregatedSummary(SessionAggregatedStatisticsDto statistics)
    {
        if (statistics.TotalBattles == 0)
        {
            ClearSessionBattlesSummary();
            return;
        }

        var winRate = statistics.TotalWins * 100d / statistics.TotalBattles;

        SessionBattlesTotalSummary = $"Всего боёв: {statistics.TotalBattles}";
        SessionBattlesWinRateSummary = $"Побед: {winRate:0.#}%";
        SessionBattlesAverageDamageSummary = $"Средний урон: {statistics.AverageDamage:0}";
        SessionBattlesAverageFragsSummary = $"Среднее количество фрагов: {statistics.AverageFrags:0.#}";
        HasSessionBattlesSummary = true;

        ApplySessionOverlaySummary(statistics.TotalBattles, winRate, statistics.AverageDamage);

        this.RaisePropertyChanged(nameof(HasSessionBattlesSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesTotalSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesWinRateSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageDamageSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageFragsSummary));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private void UpdateSessionBattlesSummary(IReadOnlyList<SessionBattleBriefDto> battles)
    {
        var finished = battles.Where(battle => battle.EndedAt is not null).ToArray();
        if (finished.Length == 0)
        {
            ClearSessionBattlesSummary();
            return;
        }

        var wins = finished.Count(battle => battle.Result is "win" or "won");
        var totalFrags = finished.Sum(battle => battle.Frags ?? 0);
        var totalDamage = finished.Sum(battle => battle.DamageDealt ?? 0);
        var winRate = wins * 100d / finished.Length;
        var averageFrags = (double)totalFrags / finished.Length;
        var averageDamage = (double)totalDamage / finished.Length;

        SessionBattlesTotalSummary = $"Всего боёв: {finished.Length}";
        SessionBattlesWinRateSummary = $"Побед: {winRate:0.#}%";
        SessionBattlesAverageDamageSummary = $"Средний урон: {averageDamage:0}";
        SessionBattlesAverageFragsSummary = $"Среднее количество фрагов: {averageFrags:0.#}";
        HasSessionBattlesSummary = true;

        ApplySessionOverlaySummary(finished.Length, winRate, averageDamage);

        this.RaisePropertyChanged(nameof(HasSessionBattlesSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesTotalSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesWinRateSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageDamageSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageFragsSummary));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private void ClearSessionBattlesSummary()
    {
        HasSessionBattlesSummary = false;
        SessionBattlesTotalSummary = string.Empty;
        SessionBattlesWinRateSummary = string.Empty;
        SessionBattlesAverageDamageSummary = string.Empty;
        SessionBattlesAverageFragsSummary = string.Empty;

        ClearSessionOverlaySummary();

        this.RaisePropertyChanged(nameof(HasSessionBattlesSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesTotalSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesWinRateSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageDamageSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageFragsSummary));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private void OnSessionBattleStarted(SessionBattleBriefDto battle) =>
        Dispatcher.UIThread.Post(() => ApplySessionBattleStarted(battle));

    private void OnSessionBattleCompleted(SessionBattleCompletedHubDto notification) =>
        Dispatcher.UIThread.Post(() => ApplySessionBattleCompleted(notification));

    private void OnSessionEnded(Guid sessionId) =>
        Dispatcher.UIThread.Post(() => _ = ApplySessionEndedAsync(sessionId));

    private void ApplySessionBattleStarted(SessionBattleBriefDto battle)
    {
        if (SelectedSession is null)
            return;

        UpsertSessionBattle(SessionBattleListItem.FromDto(battle));
    }

    private void ApplySessionBattleCompleted(SessionBattleCompletedHubDto notification)
    {
        if (SelectedSession is null)
            return;

        UpsertSessionBattle(SessionBattleListItem.FromDto(notification.Battle));
        UpdateSessionBattlesSummaryFromHub(notification.Aggregated);
    }

    private async Task ApplySessionEndedAsync(Guid sessionId)
    {
        if (SelectedSession?.Id != sessionId)
            return;

        await LoadSessionHistoryAsync(SessionHistoryPage, showBusy: false);
        await UpdateActiveSessionConnectionAsync();
    }

    private void UpsertSessionBattle(SessionBattleListItem battle)
    {
        var existingIndex = _allSessionBattles.FindIndex(item => item.Id == battle.Id);
        if (existingIndex >= 0)
            _allSessionBattles[existingIndex] = battle;
        else
            _allSessionBattles.Insert(0, battle);

        _allSessionBattles.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));
        SessionBattlesTotalCount = _allSessionBattles.Count;

        if (SessionBattlesPage > SessionBattlesTotalPages)
            SessionBattlesPage = SessionBattlesTotalPages;

        ApplySessionBattlesPage();
        RaiseSessionBattlesPagingChanged();
    }

    private void UpdateSessionBattlesSummaryFromHub(SessionBattleAggregatedHubDto aggregated)
    {
        if (aggregated.TotalBattles == 0)
        {
            ClearSessionBattlesSummary();
            return;
        }

        var winRate = aggregated.TotalWins * 100d / aggregated.TotalBattles;

        SessionBattlesTotalSummary = $"Всего боёв: {aggregated.TotalBattles}";
        SessionBattlesWinRateSummary = $"Побед: {winRate:0.#}%";
        SessionBattlesAverageDamageSummary = $"Средний урон: {aggregated.AverageDamage:0}";
        SessionBattlesAverageFragsSummary = $"Среднее количество фрагов: {aggregated.AverageFrags:0.#}";
        HasSessionBattlesSummary = true;

        ApplySessionOverlaySummary(aggregated.TotalBattles, winRate, aggregated.AverageDamage);

        this.RaisePropertyChanged(nameof(HasSessionBattlesSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesTotalSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesWinRateSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageDamageSummary));
        this.RaisePropertyChanged(nameof(SessionBattlesAverageFragsSummary));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private void ApplySessionOverlaySummary(int totalBattles, double winRate, double averageDamage)
    {
        SessionOverlayBattlesText = $"{totalBattles} боёв";
        SessionOverlayWinRateText = $"{winRate:0.#}%";
        SessionOverlayDamageText = $"{averageDamage:0} ур";
    }

    private void ClearSessionOverlaySummary()
    {
        SessionOverlayBattlesText = "—";
        SessionOverlayWinRateText = "—";
        SessionOverlayDamageText = "—";
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
