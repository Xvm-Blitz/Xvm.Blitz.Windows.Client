using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Voice;

public sealed record VoiceIceServersResponse(
    [property: JsonPropertyName("ice_servers")] IReadOnlyList<VoiceIceServerDto> IceServers,
    [property: JsonPropertyName("call_duration_seconds")] int CallDurationSeconds,
    [property: JsonPropertyName("invite_timeout_seconds")] int InviteTimeoutSeconds,
    [property: JsonPropertyName("max_participants")] int MaxParticipants,
    [property: JsonPropertyName("smaller_player_id_is_polite")] bool SmallerPlayerIdIsPolite);

public sealed record VoiceIceServerDto(
    [property: JsonPropertyName("urls")] IReadOnlyList<string> Urls,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("credential")] string? Credential);
