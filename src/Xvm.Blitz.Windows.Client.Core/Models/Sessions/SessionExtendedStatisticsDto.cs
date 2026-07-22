using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record SessionExtendedStatisticsDto(
    [property: JsonPropertyName("sessionId")] Guid SessionId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("endedAt")] DateTimeOffset? EndedAt,
    [property: JsonPropertyName("battles")] IReadOnlyList<SessionBattleBriefDto> Battles);
