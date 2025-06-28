using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Battles;

public record BattlePlayerStatistics(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("nickname")]
    string? Nickname,
    [property: JsonPropertyName("clan_tag")]
    string? ClanTag,
    [property: JsonPropertyName("tank")] string? Tank,
    [property: JsonPropertyName("table_number")]
    int TableNumber,
    [property: JsonPropertyName("win_rate_percents")]
    double? WinRatePercents,
    [property: JsonPropertyName("number_of_battles")]
    int? NumberOfBattles);