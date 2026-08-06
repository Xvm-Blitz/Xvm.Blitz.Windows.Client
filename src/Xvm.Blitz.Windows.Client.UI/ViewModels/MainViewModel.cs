using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reflection;
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

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class MainViewModel : ReactiveObject, IDisposable
{
    private static Windows_AuthorizationWindow? _currentAuthWindow;
    private static TutorialWindow? _currentTutorialWindow;

    private readonly IAppUpdateService _appUpdateService;

    private readonly IAuthorizationService _authorizationService;

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

    private string _sessionOverlayBattlesText = "-";

    private string _sessionOverlayWinRateText = "-";

    private string _sessionOverlayDamageText = "-";

    private bool _isBattleWindowsVisible = true;

    private bool _minimizeToTrayOnClose;

    private string _replaysPath;

    private string _gamePath;

    private string _loadingScreenMessage = string.Empty;

    private bool _loadingScreenIsError;

    private bool _isLoadingScreenReplaced;

    private bool _isLoadingScreenWarningVisible;

    private bool _isUpdateAvailable;

    private bool _isUpToDate;

    private bool _isDownloadingUpdate;

    private double _updateDownloadProgress;

    private string? _updateStatusMessage;

    private string? _latestVersion;

    private GetAppUpdateResponseDto? _latestUpdate;

    private SessionListItem? _selectedSession;

    private bool _isSessionBusy;

    private string? _sessionStatusMessage;

    private bool _isSessionStatusError;

    private int _sessionHistoryPage = 1;

    private int _sessionHistoryTotalCount;

    private bool _isSessionBattlesLoading;

    private Timer? _sessionStatusCountdownTimer;

    private DateTimeOffset? _sessionStatusRetryAfter;

    private bool _sessionStatusIsSessionCreateRateLimit;

    private bool _suppressSessionSelectionSideEffects;

    private int _sessionBattlesPage = 1;

    private int _sessionBattlesTotalCount;

    public string ReplaysPath
    {
        get => _replaysPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _replaysPath, value);
            _settings.ReplaysPath = value;
            AppSettings.Save(_settings);
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set
        {
            if (!this.RaiseAndSetIfChanged(ref _minimizeToTrayOnClose, value))
                return;

            _settings.MinimizeToTrayOnClose = value;
            AppSettings.Save(_settings);
        }
    }

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

    public string LoadingScreenMessage
    {
        get => _loadingScreenMessage;
        private set => this.RaiseAndSetIfChanged(ref _loadingScreenMessage, value);
    }

    public bool LoadingScreenIsError
    {
        get => _loadingScreenIsError;
        private set => this.RaiseAndSetIfChanged(ref _loadingScreenIsError, value);
    }

    public bool HasLoadingScreenMessage => !string.IsNullOrWhiteSpace(LoadingScreenMessage);

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
            if (_isDisplayConfigurationMode == value)
                return;

            if (value)
            {
                _ = ConfigureDisplayAsync();
                return;
            }

            ExitConfigurationMode();
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

    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDownloadingUpdate, value);
            this.RaisePropertyChanged(nameof(UpdateDownloadButtonText));
            this.RaisePropertyChanged(nameof(IsUpdateProgressVisible));
            this.RaisePropertyChanged(nameof(HasUpdateStatusMessage));
        }
    }

    public double UpdateDownloadProgress
    {
        get => _updateDownloadProgress;
        private set
        {
            this.RaiseAndSetIfChanged(ref _updateDownloadProgress, value);
            this.RaisePropertyChanged(nameof(UpdateDownloadProgressText));
        }
    }

    public string? UpdateStatusMessage
    {
        get => _updateStatusMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _updateStatusMessage, value);
            this.RaisePropertyChanged(nameof(HasUpdateStatusMessage));
        }
    }

    public bool HasUpdateStatusMessage =>
        !string.IsNullOrWhiteSpace(UpdateStatusMessage) && !IsDownloadingUpdate;

    public bool IsUpdateProgressVisible => IsDownloadingUpdate;

    public string UpdateDownloadProgressText => $"{UpdateDownloadProgress:0}%";

    public string UpdateDownloadButtonText => IsDownloadingUpdate ? "Скачивание…" : "Скачать и установить";

    public bool IsAuthenticated => _authorizationService.IsAuthenticated;

    public string AuthDisplayText => IsAuthenticated ? "Профиль" : "Войти";

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

    public ReactiveCommand<Unit, Unit> SelectReplaysPathCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenReplaysPathCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }

    public ReactiveCommand<Unit, Unit> ReplaceLoadingScreenCommand { get; }

    public ReactiveCommand<Unit, Unit> RestoreLoadingScreenFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> RestoreLoadingScreenDefaultsCommand { get; }

    public ReactiveCommand<Unit, Unit> ResetOverlayPositionsCommand { get; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenAuthWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    public ReactiveCommand<Unit, Unit> DownloadUpdateCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenTutorialCommand { get; }

    public ReactiveCommand<Unit, Unit> StartSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> RestoreSessionsCommand { get; }

    public ReactiveCommand<Unit, Unit> EndSessionCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousSessionHistoryPageCommand { get; }

    public ReactiveCommand<Unit, Unit> NextSessionHistoryPageCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousSessionBattlesPageCommand { get; }

    public ReactiveCommand<Unit, Unit> NextSessionBattlesPageCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshSessionBattlesCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleSessionSummaryOverlayCommand { get; }

    public ObservableCollection<SessionListItem> AvailableSessions { get; } = [];

    public ObservableCollection<SessionBattleListItem> SessionBattles { get; } = [];

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
            if (_suppressSessionSelectionSideEffects)
                return;

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
        IBattleSessionRuntimeService battleSessionRuntimeService,
        ILogger<MainViewModel> logger)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _appUpdateService = appUpdateService;
        _sessionsClient = sessionsClient;
        _usageService = usageService;
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
        _gamePath = string.IsNullOrWhiteSpace(settings.GamePath) ? AppSettings.DefaultGamePath : settings.GamePath;

        _originalAlliesWindowX = settings.AlliesWindowX;
        _originalAlliesWindowY = settings.AlliesWindowY;
        _originalEnemiesWindowX = settings.EnemiesWindowX;
        _originalEnemiesWindowY = settings.EnemiesWindowY;
        _originalSessionSummaryOverlayX = settings.SessionSummaryOverlayX;
        _originalSessionSummaryOverlayY = settings.SessionSummaryOverlayY;
        _originalSessionSummaryOverlayScaleX = _sessionSummaryOverlayScaleX;
        _originalSessionSummaryOverlayScaleY = _sessionSummaryOverlayScaleY;

        var uiScheduler = RxApp.MainThreadScheduler;

        SelectReplaysPathCommand = ReactiveCommand.CreateFromTask(SelectReplaysPath, outputScheduler: uiScheduler);
        OpenReplaysPathCommand = ReactiveCommand.Create(OpenReplaysPath, outputScheduler: uiScheduler);
        SelectGamePathCommand = ReactiveCommand.CreateFromTask(SelectGamePath, outputScheduler: uiScheduler);
        ReplaceLoadingScreenCommand = ReactiveCommand.Create(ReplaceLoadingScreen, outputScheduler: uiScheduler);
        RestoreLoadingScreenFilesCommand = ReactiveCommand.Create(RestoreLoadingScreenFiles, outputScheduler: uiScheduler);
        RestoreLoadingScreenDefaultsCommand = ReactiveCommand.Create(RestoreLoadingScreenDefaults, outputScheduler: uiScheduler);
        ResetOverlayPositionsCommand = ReactiveCommand.Create(ResetOverlayPositions, outputScheduler: uiScheduler);
        ExitCommand = ReactiveCommand.Create(Exit, outputScheduler: uiScheduler);
        OpenAuthWindowCommand = ReactiveCommand.Create(OpenAuthWindow, outputScheduler: uiScheduler);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync, outputScheduler: uiScheduler);
        DownloadUpdateCommand = ReactiveCommand.CreateFromTask(
            DownloadAndInstallUpdateAsync,
            this.WhenAnyValue(
                viewModel => viewModel.IsUpdateAvailable,
                viewModel => viewModel.IsDownloadingUpdate,
                (isAvailable, isDownloading) => isAvailable && !isDownloading),
            uiScheduler);
        OpenTutorialCommand = ReactiveCommand.Create(OpenTutorial, outputScheduler: uiScheduler);
        StartSessionCommand = ReactiveCommand.CreateFromTask(StartSessionAsync, outputScheduler: uiScheduler);
        RestoreSessionsCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(1),
            outputScheduler: uiScheduler);
        EndSessionCommand = ReactiveCommand.CreateFromTask(EndSessionAsync, outputScheduler: uiScheduler);
        PreviousSessionHistoryPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(SessionHistoryPage - 1),
            this.WhenAnyValue(viewModel => viewModel.HasPreviousSessionHistoryPage),
            uiScheduler);
        NextSessionHistoryPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionHistoryAsync(SessionHistoryPage + 1),
            this.WhenAnyValue(viewModel => viewModel.HasNextSessionHistoryPage),
            uiScheduler);
        PreviousSessionBattlesPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionBattlesAsync(SessionBattlesPage - 1),
            this.WhenAnyValue(viewModel => viewModel.HasPreviousSessionBattlesPage),
            uiScheduler);
        NextSessionBattlesPageCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionBattlesAsync(SessionBattlesPage + 1),
            this.WhenAnyValue(viewModel => viewModel.HasNextSessionBattlesPage),
            uiScheduler);
        RefreshSessionBattlesCommand = ReactiveCommand.CreateFromTask(
            () => LoadSessionBattlesAsync(),
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
        StopSessionStatusCountdown();
    }

    private void PersistSettings()
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
            _settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
            _settings.GamePath = GamePath;
            _settings.SelectedSessionId = SelectedSession?.Id;

            if (_sessionSummaryOverlayExampleApplied && !_wasSessionSummaryOverlayVisibleBeforeConfiguration)
            {
                IsSessionSummaryOverlayVisible = false;
                _settings.SessionSummaryOverlayVisible = false;
            }
            else
            {
                _settings.SessionSummaryOverlayVisible = IsSessionSummaryOverlayVisible;
            }

            if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel battleStatisticsViewModel)
                battleStatisticsViewModel.PersistPanelScale();

            AppSettings.Save(_settings);
            Dispatcher.UIThread.Post(ApplyWindowPositions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
        }
    }

    public void PersistSessionSummaryOverlayScaleAndSave()
    {
        PersistSessionSummaryOverlayScale();
        AppSettings.Save(_settings);
    }

    private void ResetOverlayPositions()
    {
        try
        {
            var defaults = new AppSettings();

            AlliesWindowX = defaults.AlliesWindowX;
            AlliesWindowY = defaults.AlliesWindowY;
            EnemiesWindowX = defaults.EnemiesWindowX;
            EnemiesWindowY = defaults.EnemiesWindowY;
            SessionSummaryOverlayX = defaults.SessionSummaryOverlayX;
            SessionSummaryOverlayY = defaults.SessionSummaryOverlayY;
            RestoreSessionSummaryOverlayScale(
                defaults.SessionSummaryOverlayScaleX,
                defaults.SessionSummaryOverlayScaleY);

            _settings.AlliesWindowX = AlliesWindowX;
            _settings.AlliesWindowY = AlliesWindowY;
            _settings.EnemiesWindowX = EnemiesWindowX;
            _settings.EnemiesWindowY = EnemiesWindowY;
            _settings.SessionSummaryOverlayX = SessionSummaryOverlayX;
            _settings.SessionSummaryOverlayY = SessionSummaryOverlayY;
            PersistSessionSummaryOverlayScale();

            if (App.AlliesWindow?.DataContext is BattleStatisticsViewModel alliesViewModel)
            {
                alliesViewModel.SetPanelScale(defaults.PanelScaleX, defaults.PanelScaleY);
                alliesViewModel.PersistPanelScale();
            }

            if (App.EnemiesWindow?.DataContext is BattleStatisticsViewModel enemiesViewModel)
                enemiesViewModel.SetPanelScale(defaults.PanelScaleX, defaults.PanelScaleY);

            AppSettings.Save(_settings);
            ApplyWindowPositions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting overlay positions");
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

    private async Task ConfigureDisplayAsync()
    {
        try
        {
            if (_isDisplayConfigurationMode)
                return;

            _originalAlliesWindowX = AlliesWindowX;
            _originalAlliesWindowY = AlliesWindowY;
            _originalEnemiesWindowX = EnemiesWindowX;
            _originalEnemiesWindowY = EnemiesWindowY;
            _originalSessionSummaryOverlayX = SessionSummaryOverlayX;
            _originalSessionSummaryOverlayY = SessionSummaryOverlayY;
            _originalSessionSummaryOverlayScaleX = _sessionSummaryOverlayScaleX;
            _originalSessionSummaryOverlayScaleY = _sessionSummaryOverlayScaleY;

            this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, true);
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
            this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, false);
            _logger.LogError(ex, "Error activating display setup mode");
        }
    }

    private void ExitConfigurationMode()
    {
        try
        {
            if (!_isDisplayConfigurationMode)
                return;

            this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, false);
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
            _currentAuthWindow.ShowInTaskbar = false;
            _currentAuthWindow.Closed += (_, _) =>
            {
                UpdateAuthStatus();
                _currentAuthWindow = null;
            };

            if (App.MainWindow is not null)
                _currentAuthWindow.Show(App.MainWindow);
            else
                _currentAuthWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening authorization window");
        }
    }

    private void UpdateAuthStatus()
    {
        this.RaisePropertyChanged(nameof(IsAuthenticated));
        this.RaisePropertyChanged(nameof(AuthDisplayText));
    }

    private static void Exit()
    {
        App.MainWindow?.Close();
    }

    private static string LoadingScreenAssetsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "BattleLoadingScreens");

    private async Task SelectGamePath()
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
                    Title = "Выберите папку с игрой Tanks Blitz",
                    AllowMultiple = false
                });

            if (folderDialog.Count > 0)
                GamePath = folderDialog[0].TryGetLocalPath() ?? GamePath;
        }
        catch (Exception ex)
        {
            ShowLoadingScreenMessage($"Ошибка при выборе папки: {ex.Message}", isError: true);
            _logger.LogError(ex, "Error selecting game path");
        }
    }

    private void ReplaceLoadingScreen()
    {
        if (!TryValidateGamePath(out _))
            return;

        try
        {
            LoadingScreenPatch.EnsureDefaultsStored(LoadingScreenAssetsDirectory);

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

            ApplyLoadingScreenDefaultsToGame();

            var optionalFont = Path.Combine(LoadingScreenPatch.DefaultsPath, "Statistics-Reader.ttf.dvpl");
            if (File.Exists(optionalFont))
            {
                File.Copy(
                    optionalFont,
                    LoadingScreenPatch.GetGameTargetPath(GamePath, "Statistics-Reader.ttf.dvpl"),
                    true);
            }

            ShowLoadingScreenMessage(
                backupExists
                    ? "Файлы экрана загрузки успешно обновлены!"
                    : "Файлы экрана загрузки успешно заменены!",
                isError: false);
            CheckLoadingScreenStatus();
        }
        catch (Exception ex)
        {
            ShowLoadingScreenMessage($"Произошла ошибка при замене файлов: {ex.Message}", isError: true);
            _logger.LogError(ex, "Error replacing loading screen files");
        }
    }

    private void RestoreLoadingScreenDefaults()
    {
        if (!TryValidateGamePath(out _))
            return;

        try
        {
            LoadingScreenPatch.EnsureDefaultsStored(LoadingScreenAssetsDirectory);
            ApplyLoadingScreenDefaultsToGame();

            var deletingFontPath = LoadingScreenPatch.GetGameTargetPath(GamePath, "Statistics-Reader.ttf.dvpl");
            if (File.Exists(deletingFontPath))
                File.Delete(deletingFontPath);

            var backupPath = LoadingScreenPatch.BackupPath;
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, true);

            ShowLoadingScreenMessage("Файлы по умолчанию успешно восстановлены!", isError: false);
            CheckLoadingScreenStatus();
        }
        catch (Exception ex)
        {
            ShowLoadingScreenMessage($"Произошла ошибка при восстановлении файлов по умолчанию: {ex.Message}", isError: true);
            _logger.LogError(ex, "Error restoring loading screen defaults");
        }
    }

    private void RestoreLoadingScreenFiles()
    {
        var backupPath = LoadingScreenPatch.BackupPath;

        if (!Directory.Exists(backupPath))
        {
            ShowLoadingScreenMessage("Резервные копии не найдены.", isError: true);
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
                File.Delete(deletingFontPath);

            Directory.Delete(backupPath, true);
            ShowLoadingScreenMessage("Файлы успешно восстановлены из резервной копии!", isError: false);
            CheckLoadingScreenStatus();
        }
        catch (Exception ex)
        {
            ShowLoadingScreenMessage($"Произошла ошибка при восстановлении файлов: {ex.Message}", isError: true);
            _logger.LogError(ex, "Error restoring loading screen files from backup");
        }
    }

    private void ApplyLoadingScreenDefaultsToGame()
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
            ShowLoadingScreenMessage("Пожалуйста, укажите путь к папке с игрой.", isError: true);
            return false;
        }

        if (!Directory.Exists(GamePath))
        {
            ShowLoadingScreenMessage("Указанная папка не существует.", isError: true);
            return false;
        }

        foreach (var folder in requiredFolders)
        {
            if (Directory.Exists(folder))
                continue;

            ShowLoadingScreenMessage($"Не найдена папка: {folder}", isError: true);
            return false;
        }

        return true;
    }

    private void ShowLoadingScreenMessage(string message, bool isError)
    {
        LoadingScreenMessage = message;
        LoadingScreenIsError = isError;
        this.RaisePropertyChanged(nameof(HasLoadingScreenMessage));
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
            _latestUpdate = updateInfo;
            IsUpdateAvailable = hasUpdate;
            IsUpToDate = !hasUpdate;
            this.RaisePropertyChanged(nameof(LatestVersionText));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error checking for application updates");
        }
    }

    private async Task DownloadAndInstallUpdateAsync()
    {
        if (_latestUpdate is null || string.IsNullOrWhiteSpace(_latestUpdate.DownloadUrl) || string.IsNullOrWhiteSpace(_latestUpdate.Version))
            return;

        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            UpdateStatusMessage = "Не удалось определить путь к текущему приложению.";
            return;
        }

        IsDownloadingUpdate = true;
        UpdateDownloadProgress = 0;
        UpdateStatusMessage = "Скачивание обновления…";

        try
        {
            Directory.CreateDirectory(AppDataPaths.UpdatesFolder);
            var safeVersion = string.Concat(
                _latestUpdate.Version.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));

            var destinationPath = Path.Combine(
                AppDataPaths.UpdatesFolder,
                $"XVMBlitz-{safeVersion}-win-x64.exe");

            var progress = new Progress<double>(value =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateDownloadProgress = value;
                });
            });

            await _appUpdateService.DownloadAsync(_latestUpdate.DownloadUrl, destinationPath, progress);

            UpdateStatusMessage = "Проверка подписи…";
            await _appUpdateService.VerifyIntegrityAsync(destinationPath, _latestUpdate);

            UpdateStatusMessage = "Установка и перезапуск…";
            _appUpdateService.ApplyUpdateAndRestart(destinationPath, currentExePath, _latestUpdate.Version);
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error downloading or installing application update");
            UpdateStatusMessage = exception is InvalidOperationException or FileNotFoundException
                ? exception.Message
                : "Не удалось скачать или установить обновление.";
            IsDownloadingUpdate = false;
            UpdateDownloadProgress = 0;
        }
    }

    private async Task InitializeSessionsAsync()
    {
        try
        {
            if (!_authorizationService.HasOpenIdSession)
                return;

            await LoadSessionHistoryAsync(1);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error initializing sessions");
        }
    }

    private async Task StartSessionAsync()
    {
        if (IsSessionBusy)
            return;

        if (!_authorizationService.HasOpenIdSession)
        {
            SetSessionStatus(HttpErrorMessages.DefaultAuthMessage, isError: true);
            return;
        }

        IsSessionBusy = true;
        SetSessionStatus("Создание сессии…", isError: false);

        try
        {
            var result = await _sessionsClient.Create();
            if (!result.IsSuccess || result.SessionId is null)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось создать сессию",
                    isError: true,
                    result.RetryAfter,
                    isSessionCreateRateLimit: result.RetryAfter is not null);
                return;
            }

            await LoadSessionHistoryAsync(1, showBusy: false, preferSessionId: result.SessionId.Value);
            if (!HasSessionStatusError)
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

    private async Task LoadSessionHistoryAsync(int page, bool showBusy = true, Guid? preferSessionId = null)
    {
        if (page < 1)
            return;

        if (showBusy && IsSessionBusy)
            return;

        if (!_authorizationService.HasOpenIdSession)
        {
            SetSessionStatus(HttpErrorMessages.DefaultAuthMessage, isError: true);
            return;
        }

        if (showBusy)
        {
            IsSessionBusy = true;
            SetSessionStatus("Загрузка истории сессий…", isError: false);
        }

        try
        {
            var result = await _sessionsClient.Restore(page, SessionHistoryPageSize);
            if (!result.IsSuccess || result.Sessions is null)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось загрузить историю сессий",
                    isError: true,
                    result.RetryAfter);
                return;
            }

            var previouslySelectedId = SelectedSession?.Id ?? _settings.SelectedSessionId;
            AvailableSessions.Clear();
            foreach (var session in result.Sessions)
                AvailableSessions.Add(new SessionListItem(session.Id, session.CreatedAt, session.EndedAt));

            SessionHistoryPage = result.Page;
            SessionHistoryTotalCount = result.TotalCount;

            _suppressSessionSelectionSideEffects = true;
            try
            {
                SelectedSession = ResolveSelectedSession(preferSessionId, previouslySelectedId);
            }
            finally
            {
                _suppressSessionSelectionSideEffects = false;
            }

            await LoadSessionBattlesAsync();
            await UpdateActiveSessionConnectionAsync();

            if (showBusy && !HasSessionStatusError)
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

    private SessionListItem? ResolveSelectedSession(Guid? preferSessionId, Guid? previouslySelectedId)
    {
        if (preferSessionId is { } preferredId)
        {
            return AvailableSessions.FirstOrDefault(item => item.Id == preferredId)
                   ?? AvailableSessions.FirstOrDefault(item => item.IsActive)
                   ?? AvailableSessions.FirstOrDefault();
        }

        return previouslySelectedId is { } selectedId
            ? AvailableSessions.FirstOrDefault(item => item.Id == selectedId)
              ?? AvailableSessions.FirstOrDefault(item => item.IsActive)
              ?? AvailableSessions.FirstOrDefault()
            : AvailableSessions.FirstOrDefault(item => item.IsActive)
              ?? AvailableSessions.FirstOrDefault();
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

        if (!_authorizationService.HasOpenIdSession)
        {
            SetSessionStatus(HttpErrorMessages.DefaultAuthMessage, isError: true);
            return;
        }

        IsSessionBusy = true;
        SetSessionStatus("Завершение сессии…", isError: false);

        try
        {
            var sessionId = SelectedSession.Id;
            var result = await _sessionsClient.End(sessionId);
            if (!result.IsSuccess)
            {
                SetSessionStatus(
                    result.ErrorMessage ?? "Не удалось завершить сессию",
                    isError: true,
                    result.RetryAfter);
                return;
            }

            await LoadSessionHistoryAsync(SessionHistoryPage, showBusy: false);
            if (!HasSessionStatusError)
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

    private void PersistSelectedSession()
    {
        _settings.SelectedSessionId = SelectedSession?.Id;
        AppSettings.Save(_settings);
    }

    private async Task UpdateActiveSessionConnectionAsync()
    {
        try
        {
            if (SelectedSession?.IsActive == true)
            {
                await _battleSessionRuntimeService.SetActiveSessionAsync(
                    SelectedSession.Id,
                    _authorizationService.TryGetLestaAccountId());
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
        SessionBattlesTotalCount = 0;
        SessionBattlesPage = 1;
        SessionBattles.Clear();
    }

    private void ApplySessionBattlesPage(
        IEnumerable<SessionBattleListItem> battles,
        int page,
        int totalCount)
    {
        SessionBattles.Clear();
        foreach (var battle in battles)
            SessionBattles.Add(battle);

        SessionBattlesTotalCount = totalCount;
        SessionBattlesPage = Math.Clamp(page, 1, SessionBattlesTotalPages);
        this.RaisePropertyChanged(nameof(HasNoSessionBattles));
        this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
    }

    private async Task LoadSessionBattlesAsync(int? page = null)
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

        if (!_authorizationService.HasOpenIdSession)
        {
            ClearSessionBattlesSource();
            ClearSessionBattlesSummary();
            SetSessionStatus(HttpErrorMessages.DefaultAuthMessage, isError: true);
            this.RaisePropertyChanged(nameof(SessionBattlesHeader));
            this.RaisePropertyChanged(nameof(HasNoSessionBattles));
            this.RaisePropertyChanged(nameof(ShowSessionStatisticsDisclaimer));
            return;
        }

        IsSessionBattlesLoading = true;

        try
        {
            try
            {
                _ = await _usageService.Get()
                    ?? throw new InvalidOperationException("Информация об использовании недоступна");
            }
            catch (Exception exception)
            {
                ClearSessionBattlesSource();
                ClearSessionBattlesSummary();
                SetSessionStatus(exception.Message, isError: true);
                return;
            }

            var targetPage = page ?? 1;

            var extendedTask = _sessionsClient.GetExtendedStatistics(
                SelectedSession.Id,
                targetPage,
                SessionBattlesPageSize);
            var aggregatedTask = _sessionsClient.GetAggregatedStatistics(SelectedSession.Id);
            await Task.WhenAll(extendedTask, aggregatedTask);

            var result = await extendedTask;
            if (!result.IsSuccess || result.Statistics is null)
            {
                ClearSessionBattlesSource();
                SetSessionStatus(result.ErrorMessage ?? "Не удалось загрузить бои сессии", isError: true);
            }
            else
            {
                ApplySessionBattlesPage(
                    result.Statistics.Battles.Select(SessionBattleListItem.FromDto),
                    result.Statistics.Page,
                    result.Statistics.TotalCount);
                ClearSessionStatus();
            }

            var aggregatedResult = await aggregatedTask;
            if (aggregatedResult is { IsSuccess: true, Statistics: not null })
                ApplyAggregatedSummary(aggregatedResult.Statistics);
            else
            {
                ClearSessionBattlesSummary();
                if (result is { IsSuccess: true, Statistics: not null })
                {
                    SetSessionStatus(
                        aggregatedResult.ErrorMessage ?? "Не удалось загрузить статистику сессии",
                        isError: true);
                }
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
        var existingIndex = -1;
        for (var index = 0; index < SessionBattles.Count; index++)
        {
            if (SessionBattles[index].Id != battle.Id)
                continue;

            existingIndex = index;
            break;
        }

        if (existingIndex >= 0)
        {
            SessionBattles[existingIndex] = battle;
            RaiseSessionBattlesPagingChanged();
            return;
        }

        SessionBattlesTotalCount++;

        if (SessionBattlesPage == 1)
        {
            SessionBattles.Insert(0, battle);
            while (SessionBattles.Count > SessionBattlesPageSize)
                SessionBattles.RemoveAt(SessionBattles.Count - 1);
        }

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
        SessionOverlayBattlesText = "-";
        SessionOverlayWinRateText = "-";
        SessionOverlayDamageText = "-";
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
