namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

public interface IAuthorizationService
{
    bool IsAuthenticated { get; }

    bool HasOpenIdSession { get; }

    Task<bool> TryRestoreSessionAsync();

    Task<bool> LoginWithOpenIdAsync(CancellationToken cancellationToken = default);

    Task<bool> ApplyAuthHeadersAsync(HttpClient httpClient, CancellationToken cancellationToken = default);

    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    long? TryGetLestaAccountId();

    Task Logout();
}
