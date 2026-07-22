using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models.Sessions;

public sealed record EndSessionRequestDto([property: JsonPropertyName("secretKey")] string SecretKey);
