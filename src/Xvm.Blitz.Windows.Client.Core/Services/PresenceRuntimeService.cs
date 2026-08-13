using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class PresenceRuntimeService(
    AppSettings settings,
    IAuthorizationService authorizationService,
    ILogger<PresenceRuntimeService> logger) : IPresenceRuntimeService, IAsyncDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _sync = new(1, 1);

    private readonly List<Action<HubConnection>> _handlerBinders = [];

    private readonly List<Func<HubConnection, CancellationToken, Task>> _afterConnectHandlers = [];

    private HubConnection? _connection;

    private CancellationTokenSource? _loopCts;

    private int _loopGeneration;

    private bool _enabled;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!authorizationService.HasOpenIdSession)
                return;

            _enabled = true;
            RestartLoop();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            _enabled = false;
            StopLoop();
            await DisconnectInternalAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    public void RegisterHandler<T>(string eventName, Func<T, Task> handler)
    {
        _handlerBinders.Add(connection => connection.On<T>(eventName, payload => handler(payload)));
    }

    public void RegisterAfterConnect(Func<HubConnection, CancellationToken, Task> handler) =>
        _afterConnectHandlers.Add(handler);

    public async Task InvokeHubAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        HubConnection? connection;
        await _sync.WaitAsync(cancellationToken);
        try
        {
            connection = _connection;
        }
        finally
        {
            _sync.Release();
        }

        if (connection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Нет соединения с сервером присутствия.");

        await connection.InvokeCoreAsync(methodName, typeof(object), args, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sync.Dispose();
    }

    private void RestartLoop()
    {
        StopLoop();
        var loopCts = new CancellationTokenSource();
        _loopCts = loopCts;
        var generation = ++_loopGeneration;
        var token = loopCts.Token;
        _ = Task.Run(() => RunLoopAsync(generation, token), token);
    }

    private void StopLoop()
    {
        var loopCts = _loopCts;
        _loopCts = null;
        if (loopCts is null)
            return;

        loopCts.Cancel();
        loopCts.Dispose();
    }

    private async Task RunLoopAsync(int generation, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && generation == _loopGeneration)
        {
            try
            {
                if (!_enabled || !authorizationService.HasOpenIdSession)
                {
                    await Task.Delay(HeartbeatInterval, cancellationToken);
                    continue;
                }

                await EnsureConnectedAsync(cancellationToken);
                await SendHeartbeatAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Presence loop iteration failed");
            }

            try
            {
                await Task.Delay(HeartbeatInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!_enabled || !authorizationService.HasOpenIdSession)
                return;

            if (_connection?.State == HubConnectionState.Connected)
                return;

            if (_connection?.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                if (await WaitForConnectedAsync(_connection, TimeSpan.FromSeconds(30), cancellationToken))
                    return;
            }

            await DisconnectInternalAsync();
            await ConnectInternalAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        var hubUrl = BuildHubUrl();
        var connection = new HubConnectionBuilder()
            .WithUrl(
                hubUrl,
                options =>
                {
                    options.AccessTokenProvider = () => authorizationService.GetAccessTokenAsync(CancellationToken.None);
                })
            .WithAutomaticReconnect(new PresenceReconnectPolicy())
            .Build();

        connection.KeepAliveInterval = TimeSpan.FromSeconds(15);
        connection.ServerTimeout = TimeSpan.FromSeconds(60);

        connection.Closed += exception =>
        {
            if (exception is not null)
                logger.LogWarning(exception, "Presence hub connection closed with error");
            else
                logger.LogInformation("Presence hub connection closed");

            return Task.CompletedTask;
        };

        connection.Reconnecting += exception =>
        {
            if (exception is not null)
                logger.LogWarning(exception, "Presence hub reconnecting");
            else
                logger.LogInformation("Presence hub reconnecting");

            return Task.CompletedTask;
        };

        connection.Reconnected += async connectionId =>
        {
            logger.LogInformation("Presence hub reconnected: {ConnectionId}", connectionId);
            await InvokeHeartbeatAsync(connection, CancellationToken.None);
            await RunAfterConnectAsync(connection, CancellationToken.None);
        };

        foreach (var binder in _handlerBinders)
            binder(connection);

        _connection = connection;
        await connection.StartAsync(cancellationToken);
        logger.LogInformation("Presence hub connected");
        await InvokeHeartbeatAsync(connection, cancellationToken);
        await RunAfterConnectAsync(connection, cancellationToken);
    }

    private async Task RunAfterConnectAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        foreach (var handler in _afterConnectHandlers)
        {
            try
            {
                await handler(connection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Presence after-connect handler failed");
            }
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        HubConnection? connection;
        await _sync.WaitAsync(cancellationToken);
        try
        {
            connection = _connection;
        }
        finally
        {
            _sync.Release();
        }

        if (connection?.State != HubConnectionState.Connected)
            return;

        await InvokeHeartbeatAsync(connection, cancellationToken);
    }

    private async Task InvokeHeartbeatAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.InvokeAsync("Heartbeat", cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Presence heartbeat failed");
            await Task.Delay(ReconnectDelay, cancellationToken);
        }
    }

    private static async Task<bool> WaitForConnectedAsync(
        HubConnection connection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.State == HubConnectionState.Connected)
                return true;

            if (connection.State == HubConnectionState.Disconnected)
                return false;

            await Task.Delay(100, cancellationToken);
        }

        return connection.State == HubConnectionState.Connected;
    }

    private async Task DisconnectInternalAsync()
    {
        if (_connection is null)
            return;

        var connection = _connection;
        _connection = null;

        try
        {
            await connection.StopAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Error stopping presence hub");
        }

        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Error disposing presence hub");
        }
    }

    private string BuildHubUrl()
    {
        var apiBaseUrl = settings.ApiBaseUrl.TrimEnd('/');

        return $"{apiBaseUrl}/v1/hubs/presence";
    }

    private sealed class PresenceReconnectPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            retryContext.PreviousRetryCount switch
            {
                0 => TimeSpan.Zero,
                1 => TimeSpan.FromSeconds(2),
                2 => TimeSpan.FromSeconds(5),
                3 => TimeSpan.FromSeconds(10),
                _ => TimeSpan.FromSeconds(30),
            };
    }
}
