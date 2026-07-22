using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class StatisticsClient(HttpClient httpClient, IAuthorizationService authorizationService, ILogger<StatisticsClient> logger) : IStatisticsClient
{
    public async Task<BattleStatisticsRequestResult> GetBattleStatistics(byte[] imageData)
    {
        try
        {
            var apiKey = await authorizationService.GetApiKey();
            if (apiKey == null)
            {
                logger.LogWarning("Failed to get a valid API key for statistics request");

                return BattleStatisticsRequestResult.ApiKeyMissing();
            }

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "file", "battleScreenshot.jpg");

            httpClient.DefaultRequestHeaders.Remove("X-Xvm-Api-Key");
            httpClient.DefaultRequestHeaders.Add("X-Xvm-Api-Key", apiKey.Key);

            var response = await httpClient.PostAsync("v1/battles/statistics", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await HttpErrorMessages.FromResponse(response)
                                   ?? HttpErrorMessages.FallbackMessageForStatus(response.StatusCode);

                logger.LogWarning(
                    "Statistics request failed: {StatusCode}. Message: {ErrorMessage}",
                    response.StatusCode,
                    errorMessage);

                return BattleStatisticsRequestResult.Failure(errorMessage, response.StatusCode);
            }

            var battleStats = await response.Content.ReadFromJsonAsync<BattleStatistics>();
            if (battleStats is null)
            {
                logger.LogWarning("Statistics response body is empty");

                return BattleStatisticsRequestResult.Failure("Не удалось распознать статистику боя");
            }

            logger.LogInformation("Battle statistics received: {@BattleStats}", battleStats);

            return BattleStatisticsRequestResult.Success(battleStats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting battle statistics");

            return BattleStatisticsRequestResult.Failure(ex.Message);
        }
    }
}
