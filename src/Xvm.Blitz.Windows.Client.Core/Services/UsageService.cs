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

public class UsageService(HttpClient httpClient, IAuthorizationService authorizationService, ILogger<UsageService> logger)
    : IUsageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<GetUsageResponseDto?> Get()
    {
        if (!await authorizationService.ApplyAuthHeadersAsync(httpClient))
        {
            logger.LogWarning("Failed to apply auth headers for quota information request");

            throw new HttpRequestException(
                HttpErrorMessages.DefaultAuthMessage,
                null,
                HttpStatusCode.Unauthorized);
        }

        var response = await httpClient.GetAsync("v1/auth/openid/usage");
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

        var quotaInfo = await response.Content.ReadFromJsonAsync<GetUsageResponseDto>(JsonOptions);
        if (quotaInfo != null)
            logger.LogInformation(
                "Usage information received: Limit: {MonthlyLimit}, Remaining: {RemainingRequests}",
                quotaInfo.TotalLimit,
                quotaInfo.TotalLimit - quotaInfo.CurrentUsage);

        return quotaInfo;
    }
}
