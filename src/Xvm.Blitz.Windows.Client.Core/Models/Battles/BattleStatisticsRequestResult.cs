using System.Net;
using Xvm.Blitz.Windows.Client.Core.Helpers;

namespace Xvm.Blitz.Windows.Client.Core.Models.Battles;

public sealed class BattleStatisticsRequestResult
{
    public BattleStatistics? Statistics { get; private init; }

    public string? ErrorMessage { get; private init; }

    public HttpStatusCode? StatusCode { get; private init; }

    public bool IsSuccess => Statistics is not null;

    public bool ShouldStopRetrying =>
        StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.PaymentRequired;

    public static BattleStatisticsRequestResult Success(BattleStatistics statistics) =>
        new() { Statistics = statistics };

    public static BattleStatisticsRequestResult Failure(string errorMessage, HttpStatusCode? statusCode = null) =>
        new()
        {
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };

    public static BattleStatisticsRequestResult ApiKeyMissing() =>
        Failure(HttpErrorMessages.DefaultApiKeyMessage);
}
