using Xvm.Blitz.Windows.Client.Core.Models.Sessions;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface ISessionsClient
{
    Task<CreateSessionResult> Create(CancellationToken cancellationToken = default);

    Task<RestoreSessionsResult> Restore(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SessionExtendedStatisticsResult> GetExtendedStatistics(
        Guid sessionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SessionAggregatedStatisticsResult> GetAggregatedStatistics(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SessionsRequestResult> End(Guid sessionId, CancellationToken cancellationToken = default);
}
