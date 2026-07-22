using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record RestoredSessionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("endedAt")] DateTimeOffset? EndedAt);
