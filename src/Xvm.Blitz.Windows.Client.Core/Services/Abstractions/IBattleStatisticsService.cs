using Xvm.Blitz.Windows.Client.Core.Models.Battles;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IBattleStatisticsService
{
    void RegisterObserver(IBattleStatisticsObserver observer);

    void UnRegisterObserver(IBattleStatisticsObserver observer);

    Task StartBattleNotify(BattleStatistics battleStatistics);

    Task EndBattleNotify();
}