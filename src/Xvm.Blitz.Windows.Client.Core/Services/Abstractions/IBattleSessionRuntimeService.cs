using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IBattleSessionRuntimeService
{
    event Action<SessionBattleBriefDto>? BattleStarted;

    event Action<SessionBattleCompletedHubDto>? BattleCompleted;

    event Action<Guid>? SessionEnded;

    Task SetActiveSessionAsync(Guid? sessionId, string? sessionNickname);

    Task NotifyBattleStartedAsync(BattleStatistics battleStatistics);
}
