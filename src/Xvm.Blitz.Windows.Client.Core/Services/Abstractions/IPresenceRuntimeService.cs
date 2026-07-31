namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IPresenceRuntimeService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
