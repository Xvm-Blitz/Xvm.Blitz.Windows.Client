using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Serilog;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Services;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.HotKeys;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public class App : Application
{
    public static readonly IServiceProvider ServiceProvider;

    private static readonly AppSettings _appSettings;

    private static NativeMenu? _trayMenu;

    private static readonly ReactiveCommand<Unit, Unit>? _restoreMainWindowCommand;

    private static readonly ReactiveCommand<Unit, Unit>? _exitApplicationCommand;

    private static TrayIcon? _trayIcon;

    public static IBattleStatisticsService BattleStatisticsService => ServiceProvider.GetRequiredService<IBattleStatisticsService>();

    public static MainWindow? MainWindow { get; private set; }

    public static AlliesWindow? AlliesWindow { get; private set; }

    public static EnemiesWindow? EnemiesWindow { get; private set; }

    static App()
    {
        var services = new ServiceCollection();
        const long maxLogFileBytes = 5 * 1024 * 1024;
        Directory.CreateDirectory(AppDataPaths.LogsFolder);
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(AppDataPaths.LogsFolder, "app.log"),
                fileSizeLimitBytes: maxLogFileBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 1)
            .CreateLogger();

        services.AddLogging(builder => { builder.AddSerilog(); });

        services.AddTransient<BattleDetectorService>();
        services.AddTransient<BattleStatisticsViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<AuthorizationViewModel>();

        services.AddSingleton<AppSettings>(_ => AppSettings.Load());
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IBattleStatisticsService, BattleStatisticsService>();
        services.AddSingleton<ISecretsStorageService, SecretsStorageService>();
        services.AddScoped<IStatisticsClient, StatisticsClient>();
        services.AddScoped<IUsageService, UsageService>();
        services.AddScoped<IAppUpdateService, AppUpdateService>();

        services.AddHttpClient<IStatisticsClient, StatisticsClient>(
            (sp, client) =>
            {
                var setting = sp.GetRequiredService<AppSettings>();

                client.BaseAddress = new Uri(setting.ApiBaseUrl);
            });

        services.AddHttpClient<IUsageService, UsageService>(
            (sp, client) =>
            {
                var setting = sp.GetRequiredService<AppSettings>();

                client.BaseAddress = new Uri(setting.ApiBaseUrl);
            });

        services.AddHttpClient<IAppUpdateService, AppUpdateService>(
            (sp, client) =>
            {
                var setting = sp.GetRequiredService<AppSettings>();

                client.BaseAddress = new Uri(setting.ApiBaseUrl);
            });

        ServiceProvider = services.BuildServiceProvider();
        ServiceProvider.GetRequiredService<IAuthorizationService>().TrySetApiKeyAsync().GetAwaiter().GetResult();

        _appSettings = ServiceProvider.GetRequiredService<AppSettings>();

        LoadingScreenPatch.EnsureDefaultsStored(
            Path.Combine(AppContext.BaseDirectory, "Assets", "BattleLoadingScreens"));

        var settings = new PacketCaptureSettings
        {
            ReplayPath = _appSettings.ReplaysPath,
            AppName = "tanksblitz"
        };

        var packetCaptureLogger = ServiceProvider.GetRequiredService<ILogger<BattleDetectorService>>();
        var apiService = ServiceProvider.GetRequiredService<IStatisticsClient>();

        var packetCaptureService = new BattleDetectorService(
            settings,
            apiService,
            BattleStatisticsService.StartBattleNotify,
            BattleStatisticsService.EndBattleNotify,
            packetCaptureLogger,
            LoadingScreenNotification.NotifyLoadingScreenRequired);

        packetCaptureService.StartDetect();

        _restoreMainWindowCommand = ReactiveCommand.Create(RestoreMainWindow);
        _exitApplicationCommand = ReactiveCommand.Create(ExitApplication);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Application started");

            var battleStatsViewModel = ServiceProvider.GetRequiredService<BattleStatisticsViewModel>();
            var settingsViewModel = new MainViewModel(
                _appSettings,
                ServiceProvider.GetRequiredService<IAuthorizationService>(),
                ServiceProvider.GetRequiredService<IAppUpdateService>(),
                ServiceProvider.GetRequiredService<ILogger<MainViewModel>>());

            MainWindow = new MainWindow
            {
                DataContext = settingsViewModel,
                AppSettings = _appSettings,
                ViewModel = settingsViewModel
            };

            CreateAlliesWindow(battleStatsViewModel);
            CreateEnemiesWindow(battleStatsViewModel);

            desktop.MainWindow = MainWindow;

            var mainWindow = MainWindow;
            if (settingsViewModel.ShouldShowTutorialOnStartup)
            {
                void OnMainWindowOpened(object? sender, EventArgs eventArgs)
                {
                    mainWindow.Opened -= OnMainWindowOpened;
                    Dispatcher.UIThread.Post(settingsViewModel.OpenTutorial, DispatcherPriority.Loaded);
                }

                mainWindow.Opened += OnMainWindowOpened;
            }

            mainWindow.Show();

            AlliesWindow!.Position = settingsViewModel.AlliesWindowPosition;
            EnemiesWindow!.UpdateLayout();

            var currentWidth = EnemiesWindow.Bounds.Width;
            var enemiesLeftX = settingsViewModel.EnemiesWindowPosition.X - (int)currentWidth;
            var enemiesTopY = settingsViewModel.EnemiesWindowPosition.Y;

            EnemiesWindow.Position = new PixelPoint(enemiesLeftX, enemiesTopY);

            AlliesWindow.Hide();
            EnemiesWindow.Hide();

            BattleStatisticsService.RegisterObserver(battleStatsViewModel);

            RegisterClosing(MainWindow, battleStatsViewModel);

            desktop.ShutdownRequested += (_, _) =>
            {
                BattleStatisticsService.UnRegisterObserver(battleStatsViewModel);
                UnregisterGlobalHotkey();
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterClosing(MainWindow mainWindow, BattleStatisticsViewModel battleStatsViewModel)
    {
        var shouldAppShutdown = true;
        mainWindow.Closing += (_, eventArgs) =>
        {
            if (_appSettings.MinimizeToTrayOnClose)
            {
                eventArgs.Cancel = true;
                mainWindow.Hide();
                ShowTrayIcon();
            }
            else
            {
                BattleStatisticsService.UnRegisterObserver(battleStatsViewModel);
                UnregisterGlobalHotkey();

                if (!shouldAppShutdown)
                    return;

                shouldAppShutdown = false;
                ExitApplication();
            }
        };
    }

    private static void RegisterGlobalHotkey()
    {
        try
        {
            GlobalHotkey.StartMonitoring(
                _appSettings.HideStatisticsHotkey,
                _appSettings.HideStatisticsCtrl,
                _appSettings.HideStatisticsAlt,
                _appSettings.HideStatisticsShift);
        }
        catch (Exception exception)
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Error starting global hotkey monitoring");
        }
    }

    private static void UnregisterGlobalHotkey()
    {
        try
        {
            GlobalHotkey.StopMonitoring();
        }
        catch (Exception exception)
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            logger.LogError(exception, "Error stopping global hotkey monitoring");
        }
    }

    public static void UpdateGlobalHotkey()
    {
        UnregisterGlobalHotkey();
        RegisterGlobalHotkey();
    }

    private static void CreateAlliesWindow(BattleStatisticsViewModel battleStatsViewModel)
    {
        AlliesWindow = new AlliesWindow
        {
            DataContext = battleStatsViewModel,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        AlliesWindow.Closed += (_, _) => RecreateAlliesWindow();
    }

    private static void CreateEnemiesWindow(BattleStatisticsViewModel battleStatsViewModel)
    {
        EnemiesWindow = new EnemiesWindow
        {
            DataContext = battleStatsViewModel,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        EnemiesWindow.Closed += (_, _) => RecreateEnemiesWindow();
    }

    public static void RecreateAlliesWindow()
    {
        var battleStatsViewModel = ServiceProvider.GetRequiredService<BattleStatisticsViewModel>();

        CreateAlliesWindow(battleStatsViewModel);
        AlliesWindow!.Position = new PixelPoint(_appSettings.AlliesWindowX, _appSettings.AlliesWindowY);

        AlliesWindow.Hide();
    }

    public static void RecreateEnemiesWindow()
    {
        var battleStatsViewModel = ServiceProvider.GetRequiredService<BattleStatisticsViewModel>();

        CreateEnemiesWindow(battleStatsViewModel);

        var currentWidth = (int)EnemiesWindow!.Bounds.Width;
        var enemiesLeftX = _appSettings.EnemiesWindowX - currentWidth;
        var enemiesTopY = _appSettings.EnemiesWindowY;
        EnemiesWindow.Position = new PixelPoint(enemiesLeftX, enemiesTopY);

        EnemiesWindow.Hide();
    }

    private static void ShowTrayIcon()
    {
        if (_trayIcon?.IsVisible == true)
            return;

        try
        {
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Xvm.Blitz.Windows.Client.UI/Assets/xvm.ico"))),
                ToolTipText = "XVM Blitz",
                IsVisible = true
            };

            _trayMenu = new NativeMenu();

            var showItem = new NativeMenuItem("Показать") { Command = _restoreMainWindowCommand };
            var exitItem = new NativeMenuItem("Выход") { Command = _exitApplicationCommand };

            _trayMenu.Add(showItem);
            _trayMenu.Add(new NativeMenuItemSeparator());
            _trayMenu.Add(exitItem);

            _trayIcon.Menu = _trayMenu;
            _trayIcon.Clicked += (_, _) => RestoreMainWindow();
        }
        catch (Exception ex)
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();

            logger.LogError(ex, "Error creating system tray");
        }
    }

    private static void RestoreMainWindow()
    {
        if (MainWindow is null)
            return;

        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();

        if (_trayIcon is null)
            return;

        _trayIcon.IsVisible = false;
        _trayIcon = null;
    }

    private static void ExitApplication()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
