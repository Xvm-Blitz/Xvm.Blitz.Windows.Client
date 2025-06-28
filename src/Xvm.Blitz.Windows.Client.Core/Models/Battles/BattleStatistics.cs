using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Battles;

public record BattleStatistics(
    [property: JsonPropertyName("allies")] IReadOnlyCollection<BattlePlayerStatistics> Allies,
    [property: JsonPropertyName("enemies")]
    IReadOnlyCollection<BattlePlayerStatistics> Enemies);