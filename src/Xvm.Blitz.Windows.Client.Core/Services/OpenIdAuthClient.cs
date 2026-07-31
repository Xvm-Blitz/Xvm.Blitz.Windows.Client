using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class OpenIdAuthClient(
    IHttpClientFactory httpClientFactory,
    AppSettings appSettings,
    ILogger<OpenIdAuthClient> logger) : IOpenIdAuthClient
{
    public const string HttpClientName = "OpenId";

    public Uri GetLoginUri() =>
        new(new Uri(appSettings.ApiBaseUrl), $"v1/auth/openid/login?client={ClientPlatform.Windows}");

    public async Task<OpenIdTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsJsonAsync(
            "v1/auth/openid/refresh",
            new OpenIdRefreshRequest(refreshToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenID refresh failed: {StatusCode}", response.StatusCode);

            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<OpenIdAuthResponse>(cancellationToken);
        if (body?.AccessToken is null || body.RefreshToken is null)
            return null;

        return new OpenIdTokens(body.AccessToken, body.RefreshToken, body.LestaExpiresAt);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/auth/openid/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("OpenID logout failed: {StatusCode}", response.StatusCode);
    }

    private sealed record OpenIdRefreshRequest(
        [property: JsonPropertyName("refresh_token")] string RefreshToken);

    private sealed record OpenIdAuthResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("lesta_expires_at")] DateTimeOffset? LestaExpiresAt);
}
