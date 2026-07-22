using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record SessionBattleCompletedHubDto(
    [property: JsonPropertyName("battle")] SessionBattleBriefDto Battle,
    [property: JsonPropertyName("aggregated")] SessionBattleAggregatedHubDto Aggregated);

public sealed record SessionBattleAggregatedHubDto(
    [property: JsonPropertyName("averageDamage")] double AverageDamage,
    [property: JsonPropertyName("averageFrags")] double AverageFrags,
    [property: JsonPropertyName("totalWins")] int TotalWins,
    [property: JsonPropertyName("totalBattles")] int TotalBattles);

public sealed record SessionEndedHubDto(
    [property: JsonPropertyName("sessionId")] Guid SessionId);
