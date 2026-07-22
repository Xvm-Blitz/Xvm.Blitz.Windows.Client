using Xvm.Blitz.Windows.Client.Core.Models.Battles;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IStatisticsClient
{
    Task<BattleStatisticsRequestResult> GetBattleStatistics(byte[] imageData);
}
