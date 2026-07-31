using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class BattleSessionRuntimeService(
    AppSettings settings,
    IAuthorizationService authorizationService,
    ILogger<BattleSessionRuntimeService> logger) : IBattleSessionRuntimeService, IAsyncDisposable
{
    private readonly SemaphoreSlim _sync = new(1, 1);

    private HubConnection? _connection;

    private CancellationTokenSource? _connectCts;

    private Guid? _activeSessionId;

    private long? _playerId;

    public event Action<SessionBattleBriefDto>? BattleStarted;

    public event Action<SessionBattleCompletedHubDto>? BattleCompleted;

    public event Action<Guid>? SessionEnded;

    public async Task SetActiveSessionAsync(Guid? sessionId, long? playerId)
    {
        await _sync.WaitAsync();
        try
        {
            if (_activeSessionId == sessionId &&
                _playerId == playerId &&
                _connection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            await DisconnectInternalAsync();

            _activeSessionId = sessionId;
            _playerId = playerId;

            if (sessionId is null || playerId is null)
                return;

            _connectCts = new CancellationTokenSource();
            try
            {
                await ConnectInternalAsync(sessionId.Value, _connectCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task NotifyBattleStartedAsync(BattleStatistics battleStatistics)
    {
        await _sync.WaitAsync();
        try
        {
            if (_activeSessionId is null || _playerId is null)
                return;

            if (!await TryEnsureConnectedAsync())
            {
                logger.LogWarning("Session hub is not connected, StartBattle skipped");

                return;
            }

            var tankName = SessionBattlePlayerResolver.ResolveTankName(_playerId.Value, battleStatistics);
            if (string.IsNullOrWhiteSpace(tankName))
            {
                logger.LogWarning(
                    "Failed to resolve tank for player {PlayerId} in battle statistics",
                    _playerId);

                return;
            }

            await _connection!.InvokeAsync("StartBattle", _activeSessionId.Value, tankName);

            logger.LogInformation(
                "StartBattle sent for session {SessionId}, tank {TankName}",
                _activeSessionId,
                tankName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error sending StartBattle to session hub");
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sync.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
        }
        finally
        {
            _sync.Release();
            _sync.Dispose();
        }
    }

    private async Task<bool> TryEnsureConnectedAsync()
    {
        if (_activeSessionId is null || _playerId is null)
            return false;

        if (_connection is null)
        {
            if (_connectCts is not null)
            {
                await _connectCts.CancelAsync();
                _connectCts.Dispose();
            }

            _connectCts = new CancellationTokenSource();

            try
            {
                await ConnectInternalAsync(_activeSessionId.Value, _connectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to restore session hub connection");

                return false;
            }

            return _connection?.State == HubConnectionState.Connected;
        }

        if (_connection.State == HubConnectionState.Connected)
            return true;

        try
        {
            if (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting &&
                await WaitForConnectedAsync(_connection, TimeSpan.FromSeconds(30)))
            {
                return true;
            }

            if (_connection.State != HubConnectionState.Disconnected)
                return false;

            logger.LogInformation(
                "Session hub disconnected, reconnecting for session {SessionId}",
                _activeSessionId);

            await _connection.StartAsync(_connectCts?.Token ?? CancellationToken.None);

            return _connection.State == HubConnectionState.Connected;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to reconnect session hub");

            return false;
        }
    }

    private static async Task<bool> WaitForConnectedAsync(HubConnection connection, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (connection.State == HubConnectionState.Connected)
                return true;

            if (connection.State == HubConnectionState.Disconnected)
                return false;

            await Task.Delay(100);
        }

        return connection.State == HubConnectionState.Connected;
    }

    private async Task ConnectInternalAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var hubUrl = BuildHubUrl(sessionId);

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
            if (exception is not null)
                logger.LogWarning(exception, "Session hub connection closed with error");

            return Task.CompletedTask;
        };

        _connection.Reconnecting += exception =>
        {
            if (exception is not null)
            {
                logger.LogWarning(exception, "Session hub reconnecting");
            }
            else
            {
                logger.LogInformation("Session hub reconnecting");
            }

            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            logger.LogInformation("Session hub reconnected: {ConnectionId}", connectionId);

            return Task.CompletedTask;
        };

        _connection.On<SessionBattleBriefDto>(
            "battleStarted",
            battle =>
            {
                logger.LogInformation("Received battleStarted for battle {BattleId}", battle.Id);
                BattleStarted?.Invoke(battle);
            });

        _connection.On<SessionBattleCompletedHubDto>(
            "battleCompleted",
            notification =>
            {
                logger.LogInformation(
                    "Received battleCompleted for battle {BattleId}",
                    notification.Battle.Id);
                BattleCompleted?.Invoke(notification);
            });

        _connection.On<SessionEndedHubDto>(
            "sessionEnded",
            notification =>
            {
                logger.LogInformation("Received sessionEnded for session {SessionId}", notification.SessionId);
                SessionEnded?.Invoke(notification.SessionId);
            });

        await _connection.StartAsync(cancellationToken);

        logger.LogInformation("Session hub connected: {SessionId}", sessionId);
    }

    private async Task DisconnectInternalAsync()
    {
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = null;

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
            logger.LogWarning(exception, "Error stopping session hub");
        }

        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Error disconnecting session hub");
        }
    }

    private string BuildHubUrl(Guid sessionId)
    {
        var apiBaseUrl = settings.ApiBaseUrl.TrimEnd('/');

        return $"{apiBaseUrl}/v1/hubs/sessions?sessionId={sessionId:D}";
    }
}
