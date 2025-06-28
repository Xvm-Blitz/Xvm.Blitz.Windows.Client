namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

public interface IAuthorizationService
{
    bool IsApiKeyExists { get; }

    Task<bool> SaveApiKey(string apiKey);

    Task<ApiKey?> GetApiKey();

    Task<bool> TrySetApiKeyAsync();

    Task Logout();
}