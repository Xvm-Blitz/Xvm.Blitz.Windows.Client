using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class HttpErrorMessages
{
    public const string DefaultAuthMessage = "Необходимо войти через Lesta OpenID";

    public const string QuotaExhaustedMessage = "Квота запросов превышена. Дождитесь обновления периода или войдите снова через Lesta OpenID";

    public const string RequestDeniedMessage = "Не удалось выполнить запрос";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string FallbackMessageForStatus(HttpStatusCode statusCode) =>
        (int)statusCode switch
        {
            401 or 403 => DefaultAuthMessage,
            400 => "Некорректный запрос",
            402 or 429 => QuotaExhaustedMessage,
            _ => RequestDeniedMessage
        };

    public static string FallbackMessageForSessionStatistics(HttpStatusCode statusCode) =>
        (int)statusCode switch
        {
            401 => DefaultAuthMessage,
            403 => "Расширенная статистика недоступна для пробного аккаунта",
            _ => "Не удалось получить статистику сессии"
        };

    public static async Task<string?> FromResponse(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default,
        bool includeRetryAfter = true)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 400 or > 499)
            return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return FromResponse(body, response, includeRetryAfter);
    }

    public static string? FromResponse(
        string body,
        HttpResponseMessage response,
        bool includeRetryAfter = true)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 400 or > 499)
            return null;

        var problemDetails = ParseProblemDetails(body);
        var baseMessage = ResolveBaseMessage(problemDetails) ??
                          (includeRetryAfter
                              ? FallbackMessageForStatus(response.StatusCode)
                              : FallbackMessageForSessionStatistics(response.StatusCode));
        if (!includeRetryAfter)
            return baseMessage;

        var retryAfter = ResolveRetryAfter(problemDetails, response.Headers.RetryAfter);
        var retryText = FormatRetryAfter(retryAfter);

        return retryText ?? baseMessage;
    }

    public static async Task<string?> FromResponseForSessionCreate(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 400 or > 499)
            return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return FromResponseForSessionCreate(body, response);
    }

    public static string? FromResponseForSessionCreate(string body, HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 400 or > 499)
            return null;

        var problemDetails = ParseProblemDetails(body);
        var retryAfter = ResolveRetryAfter(problemDetails, response.Headers.RetryAfter);
        if (FormatRetryAfter(retryAfter) is { } retryText)
            return $"Сессия не может быть создана. {retryText}";

        return ResolveBaseMessage(problemDetails) ?? FallbackMessageForStatus(response.StatusCode);
    }

    public static async Task<(string Message, bool ShouldStopRetrying)> FromBattleStatisticsResponse(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode < 400)
            return (FallbackMessageForStatus(response.StatusCode), false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var problemDetails = ParseProblemDetails(body);
        var message = ResolveBaseMessage(problemDetails)
                      ?? TryParsePlainErrorBody(body)
                      ?? FallbackMessageForStatus(response.StatusCode);

        var hasRetryAfter = ResolveRetryAfter(problemDetails, response.Headers.RetryAfter) is not null;
        var isQuotaOrRateLimit = hasRetryAfter
                                 || IsQuotaOrRateLimitType(problemDetails?.Type);
        var shouldStopRetrying = statusCode is 429 or 402 or 500
                                 || statusCode is 400 && isQuotaOrRateLimit;

        return (message, shouldStopRetrying);
    }

    private static bool IsQuotaOrRateLimitType(string? type) =>
        type is "QuotaExceeded" or "TestRateLimited";

    public static ProblemDetailsDto? ParseProblemDetails(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ProblemDetailsDto>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveBaseMessage(ProblemDetailsDto? problemDetails)
    {
        if (problemDetails is null)
            return null;

        return new[]
            {
                problemDetails.Detail,
                problemDetails.Error,
                problemDetails.Title,
                problemDetails.Reason
            }
            .FirstOrDefault(static message => !string.IsNullOrWhiteSpace(message));
    }

    private static string? TryParsePlainErrorBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var trimmed = body.Trim();
        try
        {
            var asString = JsonSerializer.Deserialize<string>(trimmed, JsonOptions);
            if (!string.IsNullOrWhiteSpace(asString))
                return asString;
        }
        catch
        {
        }

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return null;

        return trimmed.Trim('"');
    }

    public static DateTimeOffset? ResolveRetryAfter(HttpResponseMessage response, string body) =>
        ResolveRetryAfter(ParseProblemDetails(body), response.Headers.RetryAfter);

    public static string FormatRateLimitCountdown(long remainingSeconds) =>
        $"Повторите через {Math.Max(1, remainingSeconds)} секунд";

    public static string FormatSessionCreateRateLimitMessage(long remainingSeconds) =>
        $"Сессия не может быть создана. {FormatRateLimitCountdown(remainingSeconds)}";

    private static DateTimeOffset? ResolveRetryAfter(ProblemDetailsDto? problemDetails, RetryConditionHeaderValue? retryAfterHeader)
    {
        if (!string.IsNullOrWhiteSpace(problemDetails?.RetryAfter) &&
            DateTimeOffset.TryParse(problemDetails.RetryAfter, out var fromBody))
            return fromBody;

        if (retryAfterHeader?.Date is { } date)
            return date;

        if (retryAfterHeader?.Delta is { } delta)
            return DateTimeOffset.Now.Add(delta);

        return null;
    }

    private static string? FormatRetryAfter(DateTimeOffset? retryAfter)
    {
        if (retryAfter is null)
            return null;

        var now = DateTimeOffset.Now;
        if (retryAfter <= now)
            return "Можно повторить сейчас";

        var remainingSeconds = Math.Max(1, (long)(retryAfter.Value - now).TotalSeconds);
        return FormatRateLimitCountdown(remainingSeconds);
    }
}
