using Xvm.Blitz.Windows.Client.Core.Models.Battles;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IBattleStatisticsObserver
{
    Task OnBattleStatsUpdated(BattleStatistics battleStatistics);

    Task OnBattleEnded();
}