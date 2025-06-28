using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class BattleStatisticsService : IBattleStatisticsService
{
    private readonly List<IBattleStatisticsObserver> _observers = [];

    public void RegisterObserver(IBattleStatisticsObserver observer)
    {
        _observers.Add(observer);
    }

    public void UnRegisterObserver(IBattleStatisticsObserver observer)
    {
        _observers.Remove(observer);
    }

    public async Task StartBattleNotify(BattleStatistics battleStatistics)
    {
        var tasks = _observers.Select(observer => observer.OnBattleStatsUpdated(battleStatistics));

        await Task.WhenAll(tasks);
    }

    public async Task EndBattleNotify()
    {
        var tasks = _observers.Select(observer => observer.OnBattleEnded());

        await Task.WhenAll(tasks);
    }
}