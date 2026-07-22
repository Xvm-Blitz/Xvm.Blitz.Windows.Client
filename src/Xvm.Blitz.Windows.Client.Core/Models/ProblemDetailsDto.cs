using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

public sealed class ProblemDetailsDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("retryAfter")]
    public string? RetryAfter { get; init; }
}
