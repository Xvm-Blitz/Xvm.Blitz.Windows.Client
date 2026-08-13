using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.ViewModels.Models;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class BattleStatisticsViewModel(
    AppSettings settings,
    IAuthorizationService authorizationService,
    IVoiceRuntimeService voiceRuntimeService,
    ILogger<BattleStatisticsViewModel> logger) : ReactiveObject, IBattleStatisticsObserver
{
    private double _panelScaleX = OverlayPanelSizing.CoerceScaleX(settings.PanelScaleX);

    private double _panelScaleY = OverlayPanelSizing.CoerceScaleY(settings.PanelScaleY);

    private bool _isDisplayConfigurationMode;

    public ObservableCollection<CompositePlayerViewModel> Allies { get; } = new();

    public ObservableCollection<CompositePlayerViewModel> Enemies { get; } = new();

    public double PanelScaleX => _panelScaleX;

    public double PanelScaleY => _panelScaleY;

    public double OverlayFontSize => OverlayPanelSizing.FontSize(PanelScaleY);

    public double OverlayMinWidth => OverlayPanelSizing.PanelMinWidth(PanelScaleX, PanelScaleY);

    public bool IsDisplayConfigurationMode
    {
        get => _isDisplayConfigurationMode;
        set => this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, value);
    }

    private bool _voiceHandlerAttached;

    private long? _selectedCallPlayerId;

    public PlayerViewModel? SelectedCallPlayer { get; private set; }

    public bool ShowAlliesCallBar => IsSelectedIn(Allies);

    public bool ShowEnemiesCallBar => IsSelectedIn(Enemies);

    public bool CanCallSelected => SelectedCallPlayer is { CanInvite: true };

    public string SelectedCallName =>
        SelectedCallPlayer?.NicknameWithClanTag ?? SelectedCallPlayer?.Nickname ?? "игрок";

    public string SelectedCallButtonText => "Пригласить во взвод";

    public void SetPanelScale(double scaleX, double scaleY)
    {
        _panelScaleX = OverlayPanelSizing.CoerceScaleX(scaleX);
        _panelScaleY = OverlayPanelSizing.CoerceScaleY(scaleY);
        this.RaisePropertyChanged(nameof(PanelScaleX));
        this.RaisePropertyChanged(nameof(PanelScaleY));
        this.RaisePropertyChanged(nameof(OverlayFontSize));
        this.RaisePropertyChanged(nameof(OverlayMinWidth));
        Dispatcher.UIThread.Post(SyncEnemiesRightEdgeCoordinate, DispatcherPriority.Render);
    }

    public void PersistPanelScale()
    {
        settings.PanelScaleX = PanelScaleX;
        settings.PanelScaleY = PanelScaleY;
    }

    public void PersistPanelScaleAndSave()
    {
        PersistPanelScale();
        AppSettings.Save(settings);
    }

    public void RestorePanelScaleFromSettings()
    {
        SetPanelScale(settings.PanelScaleX, settings.PanelScaleY);
    }

    private static void SyncEnemiesRightEdgeCoordinate()
    {
        if (App.EnemiesWindow is null || App.MainWindow?.ViewModel is null)
            return;

        var left = App.EnemiesWindow.Position.X;
        var top = App.EnemiesWindow.Position.Y;
        var width = double.IsNaN(App.EnemiesWindow.Width) || App.EnemiesWindow.Width <= 0
            ? App.EnemiesWindow.Bounds.Width
            : App.EnemiesWindow.Width;

        App.MainWindow.ViewModel.UpdateWindowPosition(
            "Enemies",
            new PixelPoint(left + (int)Math.Round(width), top));
    }

    public async Task OnBattleStatsUpdated(BattleStatistics battleStatistics)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    if (App.MainWindow?.ViewModel is { IsDisplayConfigurationMode: true } mainViewModel)
                        mainViewModel.ConfigurationModeWithAlreadyData = true;

                    Allies.Clear();
                    var alliesOrdered = battleStatistics.Allies.ToLookup(player => player.TableNumber);

                    for (var i = 0; i < 7; i++)
                    {
                        var tableNumberAliasGroup = alliesOrdered[i].ToArray();
                        if (tableNumberAliasGroup.Length == 0)
                            Allies.Add(
                                new CompositePlayerViewModel
                                {
                                    Players =
                                    [
                                        new PlayerViewModel
                                        {
                                            TableNumber = i,
                                            IsTableNumberMissing = true
                                        }
                                    ]
                                });
                        else
                            Allies.Add(
                                new CompositePlayerViewModel
                                {
                                    Players =
                                        tableNumberAliasGroup
                                            .Select(
                                                alias => PlayerViewModel.FromStatistics(
                                                    alias,
                                                    string.IsNullOrWhiteSpace(alias.ClanTag)
                                                        ? alias.Nickname
                                                        : $"[{alias.ClanTag}] {alias.Nickname}"))
                                            .ToArray()
                                });
                    }

                    Enemies.Clear();
                    var enemiesOrdered = battleStatistics.Enemies.ToLookup(p => p.TableNumber);

                    for (var i = 0; i < 7; i++)
                    {
                        var tableNumberEnemyGroup = enemiesOrdered[i].ToArray();
                        if (tableNumberEnemyGroup.Length == 0)
                            Enemies.Add(
                                new CompositePlayerViewModel
                                {
                                    Players =
                                    [
                                        new PlayerViewModel
                                        {
                                            TableNumber = i,
                                            IsTableNumberMissing = true
                                        }
                                    ]
                                });
                        else
                            Enemies.Add(
                                new CompositePlayerViewModel
                                {
                                    Players =
                                        tableNumberEnemyGroup
                                            .Select(
                                                enemy => PlayerViewModel.FromStatistics(
                                                    enemy,
                                                    string.IsNullOrWhiteSpace(enemy.ClanTag)
                                                        ? enemy.Nickname
                                                        : $"{enemy.Nickname} [{enemy.ClanTag}]"))
                                            .ToArray()
                                });
                    }

                    UpdateWindowVisibility();

                    this.RaisePropertyChanged(nameof(Allies));
                    this.RaisePropertyChanged(nameof(Enemies));
                    RememberPlayers();
                    AttachVoice();
                    RefreshCallActions();
                });

            await Dispatcher.UIThread.InvokeAsync(ApplyWindowPositions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating UI");
        }
    }

    public Task OnBattleEnded()
    {
        Dispatcher.UIThread.InvokeAsync(
            async () =>
            {
                var mainViewModel = App.MainWindow?.ViewModel;
                if (mainViewModel?.IsDisplayConfigurationMode == true)
                {
                    mainViewModel.ConfigurationModeWithAlreadyData = false;
                    await ShowExamples();
                    return;
                }

                Allies.Clear();
                Enemies.Clear();
                ClearCallSelection();

                this.RaisePropertyChanged(nameof(Allies));
                this.RaisePropertyChanged(nameof(Enemies));

                UpdateWindowVisibility();
            });

        return Task.CompletedTask;
    }

    public void UpdateWindowVisibility()
    {
        var mainVm = App.MainWindow?.ViewModel;
        var isConfigMode = mainVm?.IsDisplayConfigurationMode == true;

        if (isConfigMode)
        {
            if (mainVm?.IsWindowsVisible == true)
            {
                ShowAlliesWindow();
                ShowEnemiesWindow();
            }
            else
            {
                App.AlliesWindow?.Hide();
                App.EnemiesWindow?.Hide();
            }

            return;
        }

        var battleWindowsAllowed = mainVm?.IsBattleWindowsVisible == true;

        if (Allies.Count > 0 && battleWindowsAllowed)
            ShowAlliesWindow();
        else
            App.AlliesWindow?.Hide();

        if (Enemies.Count > 0 && battleWindowsAllowed)
            ShowEnemiesWindow();
        else
            App.EnemiesWindow?.Hide();
    }

    public async Task ShowExamples()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    Allies.Clear();
                    Enemies.Clear();
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 0,
                                    NicknameWithClanTag = "ИгрокСОченьДлиннымИменем",
                                    Tank = "Т-54 первый образец великолепный",
                                    WinRate = 52.45,
                                    TableNumber = 0,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 999,
                                    NicknameWithClanTag = "НизкийРейтинг",
                                    Tank = "КВ-1",
                                    WinRate = 45.23,
                                    TableNumber = 1,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 1000,
                                    NicknameWithClanTag = "СреднийРейтинг",
                                    Tank = "T-34-85",
                                    WinRate = 55.78,
                                    TableNumber = 2,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 1001,
                                    NicknameWithClanTag = "ВысокийРейтинг",
                                    Tank = "ИС-7",
                                    WinRate = 65.92,
                                    TableNumber = 3,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 7000,
                                    NicknameWithClanTag = "СуперРейтинг",
                                    Tank = "Объект 140",
                                    WinRate = 75.34,
                                    TableNumber = 4,
                                    IsTableNumberMissing = false
                                },
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 7100,
                                    NicknameWithClanTag = "СреднийРейтинг",
                                    Tank = "Объект 140",
                                    WinRate = 51.56,
                                    TableNumber = 4,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });
                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 2134,
                                    NicknameWithClanTag = "СреднийРейтинг",
                                    Tank = "T62A",
                                    WinRate = 58.45,
                                    TableNumber = 5,
                                    IsTableNumberMissing = false
                                },
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 3213,
                                    NicknameWithClanTag = "СреднийРейтинг",
                                    Tank = "T62A",
                                    WinRate = 43.45,
                                    TableNumber = 5,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Allies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 2134,
                                    NicknameWithClanTag = "ИгрокБезТанка",
                                    Tank = string.Empty,
                                    WinRate = 50.00,
                                    TableNumber = 6,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 47000,
                                    NicknameWithClanTag = "VeryLongEnemyName1234567",
                                    Tank = "Maus with long description",
                                    WinRate = 51.23,
                                    TableNumber = 0,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 42000,
                                    NicknameWithClanTag = "Enemy1",
                                    Tank = "Tiger II",
                                    WinRate = 48.76,
                                    TableNumber = 1,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 17000,
                                    NicknameWithClanTag = "Enemy2",
                                    Tank = "IS-4",
                                    WinRate = 54.21,
                                    TableNumber = 2,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 45668,
                                    NicknameWithClanTag = "Enemy3",
                                    Tank = "E-100",
                                    WinRate = 62.45,
                                    TableNumber = 3,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    NumberOfBattles = 15000,
                                    NicknameWithClanTag = "Enemy4",
                                    Tank = "Jagdpanzer E-100",
                                    WinRate = 72.89,
                                    TableNumber = 4,
                                    IsTableNumberMissing = false
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    TableNumber = 5,
                                    IsTableNumberMissing = true
                                }
                            ]
                        });

                    Enemies.Add(
                        new CompositePlayerViewModel
                        {
                            Players =
                            [
                                new PlayerViewModel
                                {
                                    TableNumber = 6,
                                    IsTableNumberMissing = true
                                }
                            ]
                        });

                    UpdateWindowVisibility();

                    this.RaisePropertyChanged(nameof(Allies));
                    this.RaisePropertyChanged(nameof(Enemies));
                });

            await Dispatcher.UIThread.InvokeAsync(ApplyWindowPositions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating samples");
        }
    }

    private void ApplyWindowPositions()
    {
        if (App.EnemiesWindow == null)
            return;

        var leftX = settings.EnemiesWindowX - (int)App.EnemiesWindow.Bounds.Width;
        App.EnemiesWindow.Position = new PixelPoint(leftX, settings.EnemiesWindowY);
    }

    private static void ShowAlliesWindow()
    {
        try
        {
            if (App.AlliesWindow == null)
                App.RecreateAlliesWindow();

            App.AlliesWindow?.Show();
        }
        catch (InvalidOperationException)
        {
            App.RecreateAlliesWindow();
            App.AlliesWindow?.Show();
        }
    }

    private static void ShowEnemiesWindow()
    {
        try
        {
            if (App.EnemiesWindow == null)
                App.RecreateEnemiesWindow();

            App.EnemiesWindow?.Show();
        }
        catch (InvalidOperationException)
        {
            App.RecreateEnemiesWindow();
            App.EnemiesWindow?.Show();
        }
    }

    public bool TrySelectCallPlayer(PlayerViewModel player)
    {
        if (!player.ShowCallAction || player.PlayerId is not { } playerId || playerId <= 0)
            return false;

        _selectedCallPlayerId = _selectedCallPlayerId == playerId ? null : playerId;
        RefreshCallActions();
        return true;
    }

    public void InviteSelectedPlayer()
    {
        if (SelectedCallPlayer is { } player)
            InvitePlayer(player);
    }

    public void InvitePlayer(PlayerViewModel player)
    {
        if (player.PlayerId is not { } playerId || playerId <= 0 || !player.CanInvite)
            return;

        if (!string.IsNullOrWhiteSpace(player.Nickname))
            voiceRuntimeService.RememberPlayer(playerId, player.Nickname);

        var targetOnline = string.Equals(player.XvmUsage, "currently", StringComparison.OrdinalIgnoreCase);
        _ = voiceRuntimeService.InviteAsync(playerId, targetOnline);
        ClearCallSelection();
        RefreshCallActions();
    }

    private void AttachVoice()
    {
        if (_voiceHandlerAttached)
            return;

        _voiceHandlerAttached = true;
        voiceRuntimeService.StateChanged += (_, _) => Dispatcher.UIThread.Post(RefreshCallActions);
    }

    private void RememberPlayers()
    {
        foreach (var player in Allies.SelectMany(group => group.Players)
                     .Concat(Enemies.SelectMany(group => group.Players)))
        {
            if (player.PlayerId is { } playerId && playerId > 0 && !string.IsNullOrWhiteSpace(player.Nickname))
                voiceRuntimeService.RememberPlayer(playerId, player.Nickname);
        }
    }

    private void RefreshCallActions()
    {
        var selfId = authorizationService.TryGetLestaAccountId();
        var snapshot = voiceRuntimeService.Snapshot;
        var canStartCall = voiceRuntimeService.CanStartCall;

        foreach (var player in AllPlayers())
            ApplyCallAction(player, selfId, canStartCall, snapshot);

        SelectedCallPlayer = AllPlayers().FirstOrDefault(player => player.PlayerId == _selectedCallPlayerId && player.ShowCallAction);
        if (SelectedCallPlayer is null)
            _selectedCallPlayerId = null;

        foreach (var player in AllPlayers())
            player.IsSelected = player.PlayerId is { } id && id == _selectedCallPlayerId;

        this.RaisePropertyChanged(nameof(SelectedCallPlayer));
        this.RaisePropertyChanged(nameof(ShowAlliesCallBar));
        this.RaisePropertyChanged(nameof(ShowEnemiesCallBar));
        this.RaisePropertyChanged(nameof(CanCallSelected));
        this.RaisePropertyChanged(nameof(SelectedCallName));
        this.RaisePropertyChanged(nameof(SelectedCallButtonText));
    }

    private void ClearCallSelection()
    {
        _selectedCallPlayerId = null;
        SelectedCallPlayer = null;
        foreach (var player in AllPlayers())
            player.IsSelected = false;

        this.RaisePropertyChanged(nameof(SelectedCallPlayer));
        this.RaisePropertyChanged(nameof(ShowAlliesCallBar));
        this.RaisePropertyChanged(nameof(ShowEnemiesCallBar));
        this.RaisePropertyChanged(nameof(CanCallSelected));
        this.RaisePropertyChanged(nameof(SelectedCallName));
        this.RaisePropertyChanged(nameof(SelectedCallButtonText));
    }

    private bool IsSelectedIn(ObservableCollection<CompositePlayerViewModel> groups)
    {
        return SelectedCallPlayer?.PlayerId is { } selectedId &&
               groups.SelectMany(group => group.Players).Any(player => player.PlayerId == selectedId);
    }

    private IEnumerable<PlayerViewModel> AllPlayers()
    {
        return Allies.SelectMany(group => group.Players).Concat(Enemies.SelectMany(group => group.Players));
    }

    private static void ApplyCallAction(
        PlayerViewModel player,
        long? selfPlayerId,
        bool canStartCall,
        Core.Models.Voice.VoiceCallSnapshot snapshot)
    {
        var isSelf = player.PlayerId is { } id && selfPlayerId is { } self && id == self;
        var alreadyInRoom = player.PlayerId is { } memberId && snapshot.MemberIds.Contains(memberId);
        var show = canStartCall &&
                   player.PlayerId is > 0 &&
                   !isSelf &&
                   !player.IsTableNumberMissing &&
                   !alreadyInRoom &&
                   snapshot.CanInviteMore;

        player.ShowCallAction = show;
        player.CallActionText = "Пригласить во взвод";
        player.CanInvite = show &&
                           snapshot.Phase is Core.Models.Voice.VoiceCallPhase.Idle or Core.Models.Voice.VoiceCallPhase.Active &&
                           snapshot.OutgoingToPlayerId != player.PlayerId;
    }

    public void EraseExamples()
    {
        Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                Allies.Clear();
                Enemies.Clear();
                ClearCallSelection();
                this.RaisePropertyChanged(nameof(Allies));
                this.RaisePropertyChanged(nameof(Enemies));

                UpdateWindowVisibility();
            });
    }
}
