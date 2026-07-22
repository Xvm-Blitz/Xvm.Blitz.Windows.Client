using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record CreateSessionResponseDto([property: JsonPropertyName("id")] Guid Id);
