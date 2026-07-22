using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record RestoreSessionsResponseDto(
    [property: JsonPropertyName("sessions")] IReadOnlyList<RestoredSessionDto> Sessions,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount);
