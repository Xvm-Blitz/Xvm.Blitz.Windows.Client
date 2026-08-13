using Avalonia.Media;
using ReactiveUI;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public class PlayerViewModel : ReactiveObject
{
    private bool _showCallAction;

    private bool _canInvite;

    private bool _isSelected;

    private string _callActionText = "Пригласить во взвод";

    public long? PlayerId { get; set; }

    public string? Nickname { get; set; }

    public bool DoNotDisturb { get; set; }

    public int? NumberOfBattles { get; set; }

    public string? NicknameWithClanTag { get; set; }

    public string? Tank { get; set; }

    public double? WinRate { get; set; }

    public int TableNumber { get; set; }

    public bool IsTableNumberMissing { get; set; }

    public string? XvmUsage { get; set; }

    public IBrush XvmUsageBrush { get; set; } = Brushes.Gray;

    public bool ShowXvmUsageDot { get; set; }

    public bool ShowCallAction
    {
        get => _showCallAction;
        set => this.RaiseAndSetIfChanged(ref _showCallAction, value);
    }

    public bool CanInvite
    {
        get => _canInvite;
        set => this.RaiseAndSetIfChanged(ref _canInvite, value);
    }

    public string CallActionText
    {
        get => _callActionText;
        set => this.RaiseAndSetIfChanged(ref _callActionText, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public static PlayerViewModel FromStatistics(
        Core.Models.Battles.BattlePlayerStatistics player,
        string? nicknameWithClanTag) =>
        new()
        {
            PlayerId = player.Id,
            Nickname = player.Nickname,
            DoNotDisturb = player.DoNotDisturb,
            NumberOfBattles = player.NumberOfBattles,
            NicknameWithClanTag = nicknameWithClanTag,
            Tank = player.Tank ?? "неизвестный танк",
            WinRate = player.WinRatePercents,
            TableNumber = player.TableNumber,
            IsTableNumberMissing = false,
            XvmUsage = player.XvmUsage,
            XvmUsageBrush = ResolveXvmUsageBrush(player.XvmUsage),
            ShowXvmUsageDot = true
        };

    private static IBrush ResolveXvmUsageBrush(string? xvmUsage) =>
        xvmUsage?.Trim().ToLowerInvariant() switch
        {
            "currently" => Brushes.LimeGreen,
            "previously" => Brushes.Orange,
            _ => Brushes.Gray
        };
}
