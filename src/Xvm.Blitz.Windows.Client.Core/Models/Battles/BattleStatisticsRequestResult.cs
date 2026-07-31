using System.Net;
using Xvm.Blitz.Windows.Client.Core.Helpers;

namespace Xvm.Blitz.Windows.Client.Core.Models.Battles;

public sealed class BattleStatisticsRequestResult
{
    public BattleStatistics? Statistics { get; private init; }

    public string? ErrorMessage { get; private init; }

    public HttpStatusCode? StatusCode { get; private init; }

    public bool IsSuccess => Statistics is not null;

    public bool ShouldStopRetrying { get; private init; }

    public static BattleStatisticsRequestResult Success(BattleStatistics statistics) =>
        new() { Statistics = statistics };

    public static BattleStatisticsRequestResult Failure(
        string errorMessage,
        HttpStatusCode? statusCode = null,
        bool? shouldStopRetrying = null) =>
        new()
        {
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            ShouldStopRetrying = shouldStopRetrying ?? IsDefaultStopRetrying(statusCode)
        };

    public static BattleStatisticsRequestResult AuthMissing() =>
        Failure(HttpErrorMessages.DefaultAuthMessage, shouldStopRetrying: true);

    private static bool IsDefaultStopRetrying(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.PaymentRequired;
}
