using Microsoft.AspNetCore.SignalR.Client;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IPresenceRuntimeService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    void RegisterHandler<T>(string eventName, Func<T, Task> handler);

    void RegisterAfterConnect(Func<HubConnection, CancellationToken, Task> handler);

    Task InvokeHubAsync(string methodName, object?[] args, CancellationToken cancellationToken = default);
}
