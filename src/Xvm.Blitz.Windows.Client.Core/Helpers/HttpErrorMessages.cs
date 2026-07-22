using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class HttpErrorMessages
{
    public const string DefaultApiKeyMessage = "Необходимо настроить API ключ";

    public const string QuotaExhaustedMessage = "Квота исчерпана. Необходимо настроить API ключ";

    public const string RequestDeniedMessage = "Запрос отклонён";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string FallbackMessageForStatus(HttpStatusCode statusCode) =>
        (int)statusCode switch
        {
            401 or 403 => DefaultApiKeyMessage,
            402 or 429 => QuotaExhaustedMessage,
            _ => RequestDeniedMessage
        };

    public static string FallbackMessageForSessionStatistics(HttpStatusCode statusCode) =>
        (int)statusCode switch
        {
            401 => DefaultApiKeyMessage,
            403 => RequestDeniedMessage,
            _ => RequestDeniedMessage
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

    private static string? ResolveBaseMessage(ProblemDetailsDto? problemDetails)
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
