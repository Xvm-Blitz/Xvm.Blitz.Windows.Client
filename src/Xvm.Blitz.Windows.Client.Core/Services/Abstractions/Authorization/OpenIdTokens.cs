namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

public sealed record OpenIdTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset? LestaExpiresAt,
    DateTimeOffset? ExpiresAt = null);
