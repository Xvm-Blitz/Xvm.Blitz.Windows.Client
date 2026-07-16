using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class AppUpdateService(HttpClient httpClient, ILogger<AppUpdateService> logger) : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<GetAppUpdateResponseDto?> GetLatestVersion(
        string currentVersion,
        ClientPlatform platform,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = $"v1/updates/versions?current_version={Uri.EscapeDataString(currentVersion)}&platform={platform}";

            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to get app update info. Status code: {StatusCode}",
                    response.StatusCode);

                return null;
            }

            var updateInfo = await response.Content.ReadFromJsonAsync<GetAppUpdateResponseDto>(
                JsonOptions,
                cancellationToken);

            if (updateInfo != null)
            {
                logger.LogInformation(
                    "App update info received: Version={Version}, Platform={Platform}",
                    updateInfo.Version,
                    updateInfo.Platform);
            }

            return updateInfo;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Error getting app update information");
            return null;
        }
    }
}
