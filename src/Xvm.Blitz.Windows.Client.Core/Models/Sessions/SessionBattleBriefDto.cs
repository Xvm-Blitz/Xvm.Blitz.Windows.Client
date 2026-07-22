using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record SessionBattleBriefDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("endedAt")] DateTimeOffset? EndedAt,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("frags")] short? Frags,
    [property: JsonPropertyName("damageDealt")] short? DamageDealt,
    [property: JsonPropertyName("tankName")] string? TankName);
