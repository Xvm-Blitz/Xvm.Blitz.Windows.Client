using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

internal sealed record AuthCredentialsSecret(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("lesta_expires_at")] DateTimeOffset? LestaExpiresAt);
