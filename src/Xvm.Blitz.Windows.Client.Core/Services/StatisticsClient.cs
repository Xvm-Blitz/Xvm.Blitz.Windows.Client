using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class StatisticsClient(HttpClient httpClient, IAuthorizationService authorizationService, ILogger<StatisticsClient> logger) : IStatisticsClient
{
    public async Task<BattleStatistics?> GetBattleStatistics(byte[] imageData)
    {
        try
        {
            var apiKey = await authorizationService.GetApiKey();
            if (apiKey == null)
            {
                logger.LogWarning("Failed to get a valid API key for statistics request");

                return null;
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
                logger.LogWarning("Statistics request failed: {StatusCode}", response.StatusCode);

                return null;
            }

            var battleStats = await response.Content.ReadFromJsonAsync<BattleStatistics>();
            logger.LogInformation("Battle statistics received: {@BattleStats}", battleStats);

            return battleStats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting battle statistics");

            return null;
        }
    }
}