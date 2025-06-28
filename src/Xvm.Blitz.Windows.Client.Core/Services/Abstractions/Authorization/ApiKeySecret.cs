using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

internal sealed record ApiKeySecret([property: JsonPropertyName("key")] string Key);