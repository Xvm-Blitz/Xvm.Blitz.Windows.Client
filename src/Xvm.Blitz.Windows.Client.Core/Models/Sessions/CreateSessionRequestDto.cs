using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record CreateSessionRequestDto(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("secretKey")] string SecretKey);
