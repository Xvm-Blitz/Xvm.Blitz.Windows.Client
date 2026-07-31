using Avalonia.Media;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public class PlayerViewModel
{
    public int? NumberOfBattles { get; set; }

    public string? NicknameWithClanTag { get; set; }

    public string? Tank { get; set; }

    public double? WinRate { get; set; }

    public int TableNumber { get; set; }

    public bool IsTableNumberMissing { get; set; }

    public string? XvmUsage { get; set; }

    public IBrush XvmUsageBrush { get; set; } = Brushes.Gray;

    public bool ShowXvmUsageDot { get; set; }

    public static PlayerViewModel FromStatistics(
        Core.Models.Battles.BattlePlayerStatistics player,
        string? nicknameWithClanTag) =>
        new()
        {
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
