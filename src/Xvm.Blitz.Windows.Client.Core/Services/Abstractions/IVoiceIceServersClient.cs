using Xvm.Blitz.Windows.Client.Core.Models.Voice;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IVoiceIceServersClient
{
    Task<VoiceIceServersResponse?> GetAsync(CancellationToken cancellationToken = default);
}
