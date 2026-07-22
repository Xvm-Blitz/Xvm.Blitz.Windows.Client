namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record SessionsRequestResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    DateTimeOffset? RetryAfter = null)
{
    public static SessionsRequestResult Success() => new(true);

    public static SessionsRequestResult Failure(string message, DateTimeOffset? retryAfter = null) =>
        new(false, message, retryAfter);
}

public sealed record CreateSessionResult(
    bool IsSuccess,
    Guid? SessionId = null,
    string? ErrorMessage = null,
    DateTimeOffset? RetryAfter = null)
{
    public static CreateSessionResult Success(Guid sessionId) => new(true, sessionId);

    public static CreateSessionResult Failure(string message, DateTimeOffset? retryAfter = null) =>
        new(false, ErrorMessage: message, RetryAfter: retryAfter);
}

public sealed record RestoreSessionsResult(
    bool IsSuccess,
    IReadOnlyList<RestoredSessionDto>? Sessions = null,
    int Page = 1,
    int PageSize = 10,
    int TotalCount = 0,
    string? ErrorMessage = null,
    DateTimeOffset? RetryAfter = null)
{
    public static RestoreSessionsResult Success(
        IReadOnlyList<RestoredSessionDto> sessions,
        int page,
        int pageSize,
        int totalCount) =>
        new(true, sessions, page, pageSize, totalCount);

    public static RestoreSessionsResult Failure(string message, DateTimeOffset? retryAfter = null) =>
        new(false, ErrorMessage: message, RetryAfter: retryAfter);
}

public sealed record SessionExtendedStatisticsResult(
    bool IsSuccess,
    SessionExtendedStatisticsDto? Statistics = null,
    string? ErrorMessage = null)
{
    public static SessionExtendedStatisticsResult Success(SessionExtendedStatisticsDto statistics) => new(true, statistics);

    public static SessionExtendedStatisticsResult Failure(string message) => new(false, ErrorMessage: message);
}

public sealed record SessionAggregatedStatisticsResult(
    bool IsSuccess,
    SessionAggregatedStatisticsDto? Statistics = null,
    string? ErrorMessage = null)
{
    public static SessionAggregatedStatisticsResult Success(SessionAggregatedStatisticsDto statistics) => new(true, statistics);

    public static SessionAggregatedStatisticsResult Failure(string message) => new(false, ErrorMessage: message);
}
