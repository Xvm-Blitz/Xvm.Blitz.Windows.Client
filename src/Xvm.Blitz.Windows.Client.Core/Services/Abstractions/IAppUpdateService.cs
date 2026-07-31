using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IAppUpdateService
{
    Task<GetAppUpdateResponseDto?> GetLatestVersion(
        string currentVersion,
        ClientPlatform platform,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task VerifyIntegrityAsync(
        string filePath,
        GetAppUpdateResponseDto updateInfo,
        CancellationToken cancellationToken = default);

    void ApplyUpdateAndRestart(string downloadedExePath, string currentExePath, string newVersion);
}
