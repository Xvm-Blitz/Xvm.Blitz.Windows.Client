using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.ViewModels.Models;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class BattleStatisticsViewModel(
    AppSettings settings,
    ILogger<BattleStatisticsViewModel> logger) : ReactiveObject, IBattleStatisticsObserver
{
    private double _panelScaleX = OverlayPanelSizing.CoerceScaleX(settings.PanelScaleX);

    private double _panelScaleY = OverlayPanelSizing.CoerceScaleY(settings.PanelScaleY);

    private bool _isDisplayConfigurationMode;

    public ObservableCollection<CompositePlayerViewModel> Allies { get; } = new();

    public ObservableCollection<CompositePlayerViewModel> Enemies { get; } = new();

    public double PanelScaleX
    {
        get => _panelScaleX;
        private set
        {
            var coerced = OverlayPanelSizing.CoerceScaleX(value);
            this.RaiseAndSetIfChanged(ref _panelScaleX, coerced);
            RaiseOverlaySizingChanged();
        }
    }

    public double PanelScaleY
    {
        get => _panelScaleY;
        private set
        {
            var coerced = OverlayPanelSizing.CoerceScaleY(value);
            this.RaiseAndSetIfChanged(ref _panelScaleY, coerced);
            RaiseOverlaySizingChanged();
        }
    }

    public double OverlayFontSize => OverlayPanelSizing.FontSize(PanelScaleY);

    public double OverlayMinWidth => OverlayPanelSizing.PanelMinWidth(PanelScaleX, PanelScaleY);

    public bool IsDisplayConfigurationMode
    {
        get => _isDisplayConfigurationMode;
        set => this.RaiseAndSetIfChanged(ref _isDisplayConfigurationMode, value);
    }

    public void SetPanelScale(double scaleX, double scaleY)
    {
        _panelScaleX = OverlayPanelSizing.CoerceScaleX(scaleX);
        _panelScaleY = OverlayPanelSizing.CoerceScaleY(scaleY);
        this.RaisePropertyChanged(nameof(PanelScaleX));
        this.RaisePropertyChanged(nameof(PanelScaleY));
        RaiseOverlaySizingChanged();
        RelayoutOverlayWindows();
    }

    public void PersistPanelScale()
    {
        settings.PanelScaleX = PanelScaleX;
        settings.PanelScaleY = PanelScaleY;
    }

    public void RestorePanelScaleFromSettings()
    {
        SetPanelScale(settings.PanelScaleX, settings.PanelScaleY);
    }

    private void RaiseOverlaySizingChanged()
    {
        this.RaisePropertyChanged(nameof(OverlayFontSize));
        this.RaisePropertyChanged(nameof(OverlayMinWidth));
    }

    private static void RelayoutOverlayWindows()
    {
        RelayoutOverlayWindow(App.AlliesWindow);
        RelayoutOverlayWindow(App.EnemiesWindow);
        RepositionEnemiesWindow();
    }

    private static void RelayoutOverlayWindow(Window? window)
    {
        if (window is null)
            return;

        window.SizeToContent = SizeToContent.Manual;
        window.Width = double.NaN;
        window.Height = double.NaN;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.InvalidateMeasure();
        window.InvalidateArrange();
        window.UpdateLayout();
    }

    private static void RepositionEnemiesWindow()
    {
        if (App.EnemiesWindow is null || App.MainWindow?.ViewModel is null)
            return;

        var enemiesRightX = App.MainWindow.ViewModel.EnemiesWindowX;
        var enemiesTopY = App.MainWindow.ViewModel.EnemiesWindowY;
        App.EnemiesWindow.Position = new PixelPoint(
            enemiesRightX - (int)App.EnemiesWindow.Bounds.Width,
            enemiesTopY);
    }

    public async Task OnBattleStatsUpdated(BattleStatistics battleStatistics)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    Allies.Clear();
                    var alliesOrdered = battleStatistics.Allies.ToLookup(p => p.TableNumber);

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
                                                alias => new PlayerViewModel
                                                {
                                                    NumberOfBattles = alias.NumberOfBattles,
                                                    NicknameWithClanTag = string.IsNullOrWhiteSpace(alias.ClanTag)
                                                        ? alias.Nickname
                                                        : $"[{alias.ClanTag}] {alias.Nickname}",
                                                    Tank = alias.Tank ?? "неизвестный танк",
                                                    WinRate = alias.WinRatePercents,
                                                    TableNumber = alias.TableNumber,
                                                    IsTableNumberMissing = false
                                                })
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
                                                enemy => new PlayerViewModel
                                                {
                                                    NumberOfBattles = enemy.NumberOfBattles,
                                                    NicknameWithClanTag = string.IsNullOrWhiteSpace(enemy.ClanTag)
                                                        ? enemy.Nickname
                                                        : $"{enemy.Nickname} [{enemy.ClanTag}]",
                                                    Tank = enemy.Tank ?? "неизвестный танк",
                                                    WinRate = enemy.WinRatePercents,
                                                    TableNumber = enemy.TableNumber,
                                                    IsTableNumberMissing = false
                                                })
                                            .ToArray()
                                });
                    }

                    UpdateWindowVisibility();

                    this.RaisePropertyChanged(nameof(Allies));
                    this.RaisePropertyChanged(nameof(Enemies));
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
            () =>
            {
                Allies.Clear();
                Enemies.Clear();

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
        }
        else
        {
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

    public void EraseExamples()
    {
        Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                Allies.Clear();
                Enemies.Clear();
                this.RaisePropertyChanged(nameof(Allies));
                this.RaisePropertyChanged(nameof(Enemies));

                UpdateWindowVisibility();
            });
    }
}
