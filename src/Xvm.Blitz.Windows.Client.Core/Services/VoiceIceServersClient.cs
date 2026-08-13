using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Voice;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class VoiceIceServersClient(
    HttpClient httpClient,
    IAuthorizationService authorizationService,
    ILogger<VoiceIceServersClient> logger) : IVoiceIceServersClient
{
    public async Task<VoiceIceServersResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.ApplyAuthHeadersAsync(httpClient, cancellationToken))
        {
            logger.LogWarning("Не удалось добавить заголовки авторизации для ICE-серверов");
            return null;
        }

        var response = await httpClient.GetAsync("v1/voice/ice-servers", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await HttpErrorMessages.FromResponse(response);
            logger.LogWarning(
                "Запрос ICE-серверов не удался: {StatusCode}. {ErrorMessage}",
                response.StatusCode,
                errorMessage);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<VoiceIceServersResponse>(cancellationToken);
    }
}
