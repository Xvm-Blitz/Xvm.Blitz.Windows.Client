using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

public sealed record GetAppUpdateResponseDto(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("platform")] ClientPlatform Platform,
    [property: JsonPropertyName("sha256")] string? Sha256 = null,
    [property: JsonPropertyName("signature")] string? Signature = null);
