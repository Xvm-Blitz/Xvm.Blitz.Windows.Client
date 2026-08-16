using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

public sealed record GetUsageResponseDto(
    [property: JsonPropertyName("type")]
    AccessType Type,
    [property: JsonPropertyName("total_limit")]
    int TotalLimit,
    [property: JsonPropertyName("current_usage")]
    int CurrentUsage,
    [property: JsonPropertyName("period_start")]
    DateTimeOffset PeriodStart,
    [property: JsonPropertyName("period_end")]
    DateTimeOffset PeriodEnd,
    [property: JsonPropertyName("premium_until")]
    DateTimeOffset? PremiumUntil = null);
