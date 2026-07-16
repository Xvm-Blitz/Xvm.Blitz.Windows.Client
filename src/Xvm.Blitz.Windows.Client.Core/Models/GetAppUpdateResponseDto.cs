using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

public sealed record GetAppUpdateResponseDto(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("platform")] ClientPlatform Platform);
