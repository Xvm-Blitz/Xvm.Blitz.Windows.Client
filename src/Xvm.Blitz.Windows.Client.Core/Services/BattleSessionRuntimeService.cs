using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Models.Sessions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class BattleSessionRuntimeService(
    AppSettings settings,
    ILogger<BattleSessionRuntimeService> logger) : IBattleSessionRuntimeService, IAsyncDisposable
{
    private readonly SemaphoreSlim _sync = new(1, 1);

    private HubConnection? _connection;

    private CancellationTokenSource? _connectCts;

    private Guid? _activeSessionId;

    private string? _sessionNickname;

    public event Action<SessionBattleBriefDto>? BattleStarted;

    public event Action<SessionBattleCompletedHubDto>? BattleCompleted;

    public event Action<Guid>? SessionEnded;

    public async Task SetActiveSessionAsync(Guid? sessionId, string? sessionNickname)
    {
        var normalizedNickname = string.IsNullOrWhiteSpace(sessionNickname) ? null : sessionNickname.Trim();

        await _sync.WaitAsync();
        try
        {
            if (_activeSessionId == sessionId &&
                string.Equals(_sessionNickname, normalizedNickname, StringComparison.Ordinal) &&
                _connection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            await DisconnectInternalAsync();

            _activeSessionId = sessionId;
            _sessionNickname = normalizedNickname;

            if (sessionId is null || normalizedNickname is null)
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
            if (_activeSessionId is null || string.IsNullOrWhiteSpace(_sessionNickname))
                return;

            if (_connection?.State != HubConnectionState.Connected)
            {
                logger.LogWarning("Session hub is not connected, StartBattle skipped");

                return;
            }

            var tankName = SessionBattlePlayerResolver.ResolveTankName(_sessionNickname, battleStatistics);
            if (string.IsNullOrWhiteSpace(tankName))
            {
                logger.LogWarning(
                    "Failed to resolve tank for player {Nickname} in battle statistics",
                    _sessionNickname);

                return;
            }

            await _connection.InvokeAsync("StartBattle", _activeSessionId.Value, tankName);

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

    private async Task ConnectInternalAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var hubUrl = BuildHubUrl(sessionId);

        _connection = new HubConnectionBuilder()
            .WithUrl(
                hubUrl,
                options =>
                {
                    if (hubUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                        };
                    }
                })
            .Build();

        _connection.Closed += exception =>
        {
            if (exception is not null)
                logger.LogWarning(exception, "Session hub connection closed with error");

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
