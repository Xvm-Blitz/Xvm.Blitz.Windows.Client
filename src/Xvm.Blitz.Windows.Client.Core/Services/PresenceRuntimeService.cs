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
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _sync = new(1, 1);

    private HubConnection? _connection;

    private CancellationTokenSource? _heartbeatCts;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!authorizationService.HasOpenIdSession)
                return;

            if (_connection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            await DisconnectInternalAsync();

            var hubUrl = BuildHubUrl();
            _connection = new HubConnectionBuilder()
                .WithUrl(
                    hubUrl,
                    options =>
                    {
                        options.AccessTokenProvider = () => authorizationService.GetAccessTokenAsync(CancellationToken.None);
                        if (hubUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                        {
                            options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback =
                                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                            };
                        }
                    })
                .WithAutomaticReconnect()
                .Build();

            _connection.Closed += exception =>
            {
                StopHeartbeat();
                if (exception is not null)
                    logger.LogWarning(exception, "Presence hub connection closed with error");

                return Task.CompletedTask;
            };

            _connection.Reconnecting += exception =>
            {
                StopHeartbeat();
                if (exception is not null)
                    logger.LogWarning(exception, "Presence hub reconnecting");
                else
                    logger.LogInformation("Presence hub reconnecting");

                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                logger.LogInformation("Presence hub reconnected: {ConnectionId}", connectionId);
                StartHeartbeat(_connection);

                return Task.CompletedTask;
            };

            await _connection.StartAsync(cancellationToken);
            logger.LogInformation("Presence hub connected");
            StartHeartbeat(_connection);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to connect presence hub");
            await DisconnectInternalAsync();
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
            await DisconnectInternalAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sync.Dispose();
    }

    private void StartHeartbeat(HubConnection? connection)
    {
        StopHeartbeat();
        if (connection is null)
            return;

        var heartbeatCts = new CancellationTokenSource();
        _heartbeatCts = heartbeatCts;
        var token = heartbeatCts.Token;

        _ = Task.Run(
            async () =>
            {
                using var timer = new PeriodicTimer(HeartbeatInterval);
                while (await timer.WaitForNextTickAsync(token))
                {
                    try
                    {
                        if (connection.State != HubConnectionState.Connected)
                            continue;

                        await connection.InvokeAsync("Heartbeat", cancellationToken: token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        logger.LogDebug(exception, "Presence heartbeat failed");
                    }
                }
            },
            token);
    }

    private void StopHeartbeat()
    {
        var heartbeatCts = _heartbeatCts;
        _heartbeatCts = null;
        if (heartbeatCts is null)
            return;

        heartbeatCts.Cancel();
        heartbeatCts.Dispose();
    }

    private async Task DisconnectInternalAsync()
    {
        StopHeartbeat();

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
}
