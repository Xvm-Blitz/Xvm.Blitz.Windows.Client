using Xvm.Blitz.Windows.Client.Core.Models.Battles;

namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class SessionBattlePlayerResolver
{
    public static string? ResolveTankName(string sessionNickname, BattleStatistics battleStatistics)
    {
        if (string.IsNullOrWhiteSpace(sessionNickname))
            return null;

        var player = battleStatistics.Allies.FirstOrDefault(
            ally => NicknamesMatch(sessionNickname, ally.Nickname));

        return string.IsNullOrWhiteSpace(player?.Tank) ? null : player.Tank.Trim();
    }

    private static bool NicknamesMatch(string sessionNickname, string? playerNickname) =>
        string.Equals(sessionNickname.Trim(), playerNickname?.Trim(), StringComparison.OrdinalIgnoreCase);
}
