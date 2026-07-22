using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record SessionAggregatedStatisticsDto(
    [property: JsonPropertyName("sessionId")] Guid SessionId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("endedAt")] DateTimeOffset? EndedAt,
    [property: JsonPropertyName("averageDamage")] double AverageDamage,
    [property: JsonPropertyName("averageFrags")] double AverageFrags,
    [property: JsonPropertyName("totalWins")] int TotalWins,
    [property: JsonPropertyName("totalBattles")] int TotalBattles);
