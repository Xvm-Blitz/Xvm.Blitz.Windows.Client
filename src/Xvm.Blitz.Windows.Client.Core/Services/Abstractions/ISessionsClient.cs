using Xvm.Blitz.Windows.Client.Core.Models.Sessions;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface ISessionsClient
{
    Task<CreateSessionResult> Create(string nickname, string secretKey, CancellationToken cancellationToken = default);

    Task<RestoreSessionsResult> Restore(
        string nickname,
        string secretKey,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SessionExtendedStatisticsResult> GetExtendedStatistics(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SessionAggregatedStatisticsResult> GetAggregatedStatistics(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SessionsRequestResult> End(Guid sessionId, string secretKey, CancellationToken cancellationToken = default);
}
