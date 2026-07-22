using Avalonia.Media;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public sealed class SessionBattleListItem
{
    private static readonly IBrush WinResultBackground = new SolidColorBrush(Color.Parse("#1E3D24"));

    private static readonly IBrush LossResultBackground = new SolidColorBrush(Color.Parse("#3D1E1E"));

    public long Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string TankName { get; init; } = "—";

    public string ResultText { get; init; } = "—";

    public IBrush ResultBackground { get; init; } = Brushes.Transparent;

    public string FragsText { get; init; } = "—";

    public string DamageText { get; init; } = "—";

    public string StartedAtText => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    public static SessionBattleListItem FromDto(SessionBattleBriefDto battle) =>
        new()
        {
            Id = battle.Id,
            CreatedAt = battle.CreatedAt,
            TankName = string.IsNullOrWhiteSpace(battle.TankName) ? "—" : battle.TankName,
            ResultText = FormatResult(battle.Result, battle.EndedAt),
            ResultBackground = ResolveResultBackground(battle.Result, battle.EndedAt),
            FragsText = battle.Frags?.ToString() ?? "—",
            DamageText = battle.DamageDealt?.ToString() ?? "—",
        };

    private static IBrush ResolveResultBackground(string? result, DateTimeOffset? endedAt)
    {
        if (endedAt is null)
            return Brushes.Transparent;

        return result switch
        {
            "win" or "won" => WinResultBackground,
            "loss" or "lost" => LossResultBackground,
            _ => Brushes.Transparent,
        };
    }

    private static string FormatResult(string? result, DateTimeOffset? endedAt)
    {
        if (endedAt is null)
            return "В бою";

        return result switch
        {
            "win" or "won" => "Победа",
            "loss" or "lost" => "Поражение",
            "draw" => "Ничья",
            _ => "—",
        };
    }
}
