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
                logger.LogWarning("Не удалось получить действительный API ключ для запроса статистики");

                return null;
            }

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageData);
            content.Add(imageContent, "file", "battleScreenshot.png");

            httpClient.DefaultRequestHeaders.Remove("X-Xvm-Api-Key");
            httpClient.DefaultRequestHeaders.Add("X-Xvm-Api-Key", apiKey.Key);

            var response = await httpClient.PostAsync("v1/battles/statistics", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ошибка запроса статистики: {StatusCode}", response.StatusCode);

                return null;
            }

            var battleStats = await response.Content.ReadFromJsonAsync<BattleStatistics>();
            logger.LogInformation("Получена статистика боя: {@BattleStats}", battleStats);

            return battleStats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики боя");

            return null;
        }
    }
}