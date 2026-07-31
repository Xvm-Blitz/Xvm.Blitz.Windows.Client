namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

public interface IOpenIdAuthClient
{
    Uri GetLoginUri();

    Task<OpenIdTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default);
}
