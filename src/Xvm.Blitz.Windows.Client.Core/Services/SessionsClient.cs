using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class SessionsClient(
    HttpClient httpClient,
    IAuthorizationService authorizationService,
    ILogger<SessionsClient> logger) : ISessionsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CreateSessionResult> Create(string nickname, string secretKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "v1/sessions",
                new CreateSessionRequestDto(nickname, secretKey),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var (errorMessage, retryAfter) = await ReadCreateSessionErrorMessage(response, cancellationToken);
                logger.LogWarning("Create session failed: {StatusCode}. {ErrorMessage}", response.StatusCode, errorMessage);

                return CreateSessionResult.Failure(errorMessage, retryAfter);
            }

            var body = await response.Content.ReadFromJsonAsync<CreateSessionResponseDto>(JsonOptions, cancellationToken);
            if (body is null)
                return CreateSessionResult.Failure("Пустой ответ сервера");

            return CreateSessionResult.Success(body.Id);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error creating session");

            return CreateSessionResult.Failure(exception.Message);
        }
    }

    public async Task<RestoreSessionsResult> Restore(
        string nickname,
        string secretKey,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url =
                $"v1/sessions/restore?nickname={Uri.EscapeDataString(nickname)}&secret_key={Uri.EscapeDataString(secretKey)}&page={page}&page_size={pageSize}";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var (errorMessage, retryAfter) = await ReadErrorMessage(response, cancellationToken);
                logger.LogWarning("Restore sessions failed: {StatusCode}. {ErrorMessage}", response.StatusCode, errorMessage);

                return RestoreSessionsResult.Failure(errorMessage, retryAfter);
            }

            var body = await response.Content.ReadFromJsonAsync<RestoreSessionsResponseDto>(JsonOptions, cancellationToken);
            if (body is null)
                return RestoreSessionsResult.Failure("Пустой ответ сервера");

            return RestoreSessionsResult.Success(body.Sessions, body.Page, body.PageSize, body.TotalCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error restoring sessions");

            return RestoreSessionsResult.Failure(exception.Message);
        }
    }

    public async Task<SessionExtendedStatisticsResult> GetExtendedStatistics(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await TryApplyApiKeyHeaderAsync())
                return SessionExtendedStatisticsResult.Failure(HttpErrorMessages.DefaultApiKeyMessage);

            var url = $"v1/sessions/statistics/extended?uuid={sessionId:D}";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ReadSessionStatisticsErrorMessage(response, cancellationToken);
                logger.LogWarning(
                    "Get session extended statistics failed: {StatusCode}. {ErrorMessage}",
                    response.StatusCode,
                    errorMessage);

                return SessionExtendedStatisticsResult.Failure(errorMessage);
            }

            var body = await response.Content.ReadFromJsonAsync<SessionExtendedStatisticsDto[]>(JsonOptions, cancellationToken);
            if (body is null || body.Length == 0)
                return SessionExtendedStatisticsResult.Failure("Сессия не найдена");

            return SessionExtendedStatisticsResult.Success(body[0]);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error getting session extended statistics");

            return SessionExtendedStatisticsResult.Failure(exception.Message);
        }
    }

    public async Task<SessionAggregatedStatisticsResult> GetAggregatedStatistics(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await TryApplyApiKeyHeaderAsync())
                return SessionAggregatedStatisticsResult.Failure(HttpErrorMessages.DefaultApiKeyMessage);

            var url = $"v1/sessions/statistics/aggregated?uuid={sessionId:D}";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ReadSessionStatisticsErrorMessage(response, cancellationToken);
                logger.LogWarning(
                    "Get session aggregated statistics failed: {StatusCode}. {ErrorMessage}",
                    response.StatusCode,
                    errorMessage);

                return SessionAggregatedStatisticsResult.Failure(errorMessage);
            }

            var body = await response.Content.ReadFromJsonAsync<SessionAggregatedStatisticsDto[]>(JsonOptions, cancellationToken);
            if (body is null || body.Length == 0)
                return SessionAggregatedStatisticsResult.Failure("Сессия не найдена");

            return SessionAggregatedStatisticsResult.Success(body[0]);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error getting session aggregated statistics");

            return SessionAggregatedStatisticsResult.Failure(exception.Message);
        }
    }

    public async Task<SessionsRequestResult> End(Guid sessionId, string secretKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                $"v1/sessions/{sessionId}/end",
                new EndSessionRequestDto(secretKey),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var (errorMessage, retryAfter) = await ReadErrorMessage(response, cancellationToken);
                logger.LogWarning("End session failed: {StatusCode}. {ErrorMessage}", response.StatusCode, errorMessage);

                return SessionsRequestResult.Failure(errorMessage, retryAfter);
            }

            return SessionsRequestResult.Success();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error ending session");

            return SessionsRequestResult.Failure(exception.Message);
        }
    }

    private async Task<bool> TryApplyApiKeyHeaderAsync()
    {
        var apiKey = await authorizationService.GetApiKey();
        if (apiKey is null)
            return false;

        httpClient.DefaultRequestHeaders.Remove("X-Xvm-Api-Key");
        httpClient.DefaultRequestHeaders.Add("X-Xvm-Api-Key", apiKey.Key);

        return true;
    }

    private static async Task<string> ReadSessionStatisticsErrorMessage(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fromProblem = await HttpErrorMessages.FromResponse(response, cancellationToken, includeRetryAfter: false);
        if (!string.IsNullOrWhiteSpace(fromProblem))
            return fromProblem;

        return HttpErrorMessages.FallbackMessageForSessionStatistics(response.StatusCode);
    }

    private static async Task<(string Message, DateTimeOffset? RetryAfter)> ReadCreateSessionErrorMessage(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var retryAfter = HttpErrorMessages.ResolveRetryAfter(response, body);
        if (retryAfter is { } retryAfterValue && retryAfterValue > DateTimeOffset.Now)
        {
            var remainingSeconds = (long)(retryAfterValue - DateTimeOffset.Now).TotalSeconds;
            return (HttpErrorMessages.FormatSessionCreateRateLimitMessage(remainingSeconds), retryAfterValue);
        }

        var fromProblem = HttpErrorMessages.FromResponseForSessionCreate(body, response);
        if (!string.IsNullOrWhiteSpace(fromProblem))
            return (fromProblem, null);

        try
        {
            if (string.IsNullOrWhiteSpace(body))
                return (HttpErrorMessages.FallbackMessageForStatus(response.StatusCode), null);

            var payload = JsonSerializer.Deserialize<ErrorPayload>(body, JsonOptions);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
                return (payload.Message, null);
        }
        catch
        {
        }

        return (HttpErrorMessages.FallbackMessageForStatus(response.StatusCode), null);
    }

    private static async Task<(string Message, DateTimeOffset? RetryAfter)> ReadErrorMessage(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var retryAfter = HttpErrorMessages.ResolveRetryAfter(response, body);
        if (retryAfter is { } retryAfterValue && retryAfterValue > DateTimeOffset.Now)
        {
            var remainingSeconds = (long)(retryAfterValue - DateTimeOffset.Now).TotalSeconds;
            return (HttpErrorMessages.FormatRateLimitCountdown(remainingSeconds), retryAfterValue);
        }

        var fromProblem = HttpErrorMessages.FromResponse(body, response);
        if (!string.IsNullOrWhiteSpace(fromProblem))
            return (fromProblem, null);

        try
        {
            if (string.IsNullOrWhiteSpace(body))
                return (HttpErrorMessages.FallbackMessageForStatus(response.StatusCode), null);

            var payload = JsonSerializer.Deserialize<ErrorPayload>(body, JsonOptions);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
                return (payload.Message, null);
        }
        catch
        {
        }

        return (HttpErrorMessages.FallbackMessageForStatus(response.StatusCode), null);
    }

    private sealed record ErrorPayload([property: JsonPropertyName("message")] string? Message);
}
