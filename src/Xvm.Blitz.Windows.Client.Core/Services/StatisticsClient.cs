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
            if (!await authorizationService.ApplyAuthHeadersAsync(httpClient))
            {
                logger.LogWarning("Failed to apply auth headers for statistics request");

                return BattleStatisticsRequestResult.AuthMissing();
            }

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "file", "battleScreenshot.jpg");

            var response = await httpClient.PostAsync("v1/battles/statistics", content);

            if (!response.IsSuccessStatusCode)
            {
                var (errorMessage, shouldStopRetrying) = await HttpErrorMessages.FromBattleStatisticsResponse(response);

                logger.LogWarning(
                    "Statistics request failed: {StatusCode}. Message: {ErrorMessage}",
                    response.StatusCode,
                    errorMessage);

                return BattleStatisticsRequestResult.Failure(errorMessage, response.StatusCode, shouldStopRetrying);
            }

            var battleStats = await response.Content.ReadFromJsonAsync<BattleStatistics>();
            if (battleStats is null)
            {
                logger.LogWarning("Statistics response body is empty");

                return BattleStatisticsRequestResult.Failure("Не удалось распознать статистику боя", shouldStopRetrying: true);
            }

            logger.LogInformation("Battle statistics received: {@BattleStats}", battleStats);

            return BattleStatisticsRequestResult.Success(battleStats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting battle statistics");

            return BattleStatisticsRequestResult.Failure(ex.Message, shouldStopRetrying: true);
        }
    }
}
