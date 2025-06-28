using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class AuthorizationService(ISecretsStorageService secretsStorage, ILogger<AuthorizationService> logger) : IAuthorizationService
{
    private static ApiKey? _apiKey;

    public bool IsApiKeyExists => _apiKey is not null;

    public Task<ApiKey?> GetApiKey()
    {
        if (_apiKey != null)
            return Task.FromResult<ApiKey?>(_apiKey);

        logger.LogWarning("Не авторизован");

        return Task.FromResult<ApiKey?>(null);
    }

    public async Task<bool> SaveApiKey(string apiKey)
    {
        _apiKey = new ApiKey(apiKey);
        var apiKeySecret = new ApiKeySecret(_apiKey.Key);

        await secretsStorage.Save(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(apiKeySecret)));

        return true;
    }

    public async Task<bool> TrySetApiKeyAsync()
    {
        try
        {
            var secretBytes = await secretsStorage.Load();
            if (secretBytes == null)
                return false;

            var json = Encoding.UTF8.GetString(secretBytes);
            var secret = JsonSerializer.Deserialize<ApiKeySecret>(json);
            if (secret is null)
                return false;

            _apiKey = new ApiKey(secret.Key);

            return true;
        }
        catch (Exception)
        {
            await secretsStorage.Clear();
            _apiKey = null;

            return false;
        }
    }

    public async Task Logout()
    {
        _apiKey = null;
        await secretsStorage.Clear();

        logger.LogInformation("Выполнен выход из системы, секреты удалены");
    }
}