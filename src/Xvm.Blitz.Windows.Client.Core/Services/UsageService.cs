using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class UsageService(HttpClient httpClient, IAuthorizationService authorizationService, ILogger<UsageService> logger)
    : IUsageService
{
    public async Task<GetUsageResponseDto?> Get()
    {
        var apiKey = await authorizationService.GetApiKey();
        if (apiKey == null)
        {
            logger.LogWarning("Failed to get a valid API key for quota information request");

            throw new HttpRequestException(
                HttpErrorMessages.DefaultApiKeyMessage,
                null,
                HttpStatusCode.Unauthorized);
        }

        httpClient.DefaultRequestHeaders.Remove("X-Xvm-Api-Key");
        httpClient.DefaultRequestHeaders.Add("X-Xvm-Api-Key", apiKey.Key);

        var response = await httpClient.GetAsync("v1/api_keys/usage");
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response)
                               ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

            logger.LogWarning(
                "Usage request failed: {StatusCode}. Message: {ErrorMessage}",
                response.StatusCode,
                errorMessage);

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        var quotaInfo = await response.Content.ReadFromJsonAsync<GetUsageResponseDto>();
        if (quotaInfo != null)
            logger.LogInformation(
                "Usage information received: Limit: {MonthlyLimit}, Remaining: {RemainingRequests}",
                quotaInfo.TotalLimit,
                quotaInfo.TotalLimit - quotaInfo.CurrentUsage);

        return quotaInfo;
    }
}
