using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IAppUpdateService
{
    Task<GetAppUpdateResponseDto?> GetLatestVersion(
        string currentVersion,
        ClientPlatform platform,
        CancellationToken cancellationToken = default);
}
