using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class SubscriptionService(
    HttpClient httpClient,
    IAuthorizationService authorizationService,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<GetSubscriptionPublicPricingResponseDto?> GetPublicPricingAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("v1/subscriptions/pricing", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response, cancellationToken)
                               ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

            logger.LogWarning("Public subscription pricing request failed: {StatusCode}. Message: {ErrorMessage}", response.StatusCode, errorMessage);

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<GetSubscriptionPublicPricingResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<GetSubscriptionUserPricingResponseDto?> GetUserPricingAsync(CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.ApplyAuthHeadersAsync(httpClient, cancellationToken))
        {
            throw new HttpRequestException(HttpErrorMessages.DefaultAuthMessage, null, HttpStatusCode.Unauthorized);
        }

        var response = await httpClient.GetAsync("v1/subscriptions/pricing/me", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response, cancellationToken)
                               ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

            logger.LogWarning("Subscription pricing request failed: {StatusCode}. Message: {ErrorMessage}", response.StatusCode, errorMessage);

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<GetSubscriptionUserPricingResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<CreateSubscriptionPaymentResponseDto?> CreatePaymentAsync(CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.ApplyAuthHeadersAsync(httpClient, cancellationToken))
        {
            throw new HttpRequestException(HttpErrorMessages.DefaultAuthMessage, null, HttpStatusCode.Unauthorized);
        }

        var response = await httpClient.PostAsync("v1/subscriptions/payments", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response, cancellationToken)
                               ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

            logger.LogWarning("Subscription payment creation failed: {StatusCode}. Message: {ErrorMessage}", response.StatusCode, errorMessage);

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<CreateSubscriptionPaymentResponseDto>(JsonOptions, cancellationToken);
    }

    public async Task<GetSubscriptionPaymentResponseDto?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.ApplyAuthHeadersAsync(httpClient, cancellationToken))
        {
            throw new HttpRequestException(HttpErrorMessages.DefaultAuthMessage, null, HttpStatusCode.Unauthorized);
        }

        var response = await httpClient.GetAsync($"v1/subscriptions/payments/{paymentId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response)
                               ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

            logger.LogWarning("Subscription payment status request failed: {StatusCode}. Message: {ErrorMessage}", response.StatusCode, errorMessage);

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<GetSubscriptionPaymentResponseDto>(JsonOptions, cancellationToken);
    }
}
