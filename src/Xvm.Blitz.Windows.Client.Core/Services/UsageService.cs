using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class UsageService(HttpClient httpClient, IAuthorizationService authorizationService, ILogger<UsageService> logger)
    : IUsageService
{
    public async Task<GetUsageResponseDto?> Get()
    {
        try
        {
            var apiKey = await authorizationService.GetApiKey();
            if (apiKey == null)
            {
                logger.LogWarning("Не удалось получить действительный API ключ для запроса информации о квоте");

                return null;
            }

            httpClient.DefaultRequestHeaders.Remove("X-Xvm-Api-Key");
            httpClient.DefaultRequestHeaders.Add("X-Xvm-Api-Key", apiKey.Key);

            var response = await httpClient.GetAsync("v1/api_keys/usage");
            response.EnsureSuccessStatusCode();

            var quotaInfo = await response.Content.ReadFromJsonAsync<GetUsageResponseDto>();
            if (quotaInfo != null)
                logger.LogInformation(
                    "Получена информация об использовании: Лимит: {MonthlyLimit}, Осталось: {RemainingRequests}",
                    quotaInfo.TotalLimit,
                    quotaInfo.TotalLimit - quotaInfo.CurrentUsage);

            return quotaInfo;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Ошибка при получении информации об использовании");
            return null;
        }
    }
}
