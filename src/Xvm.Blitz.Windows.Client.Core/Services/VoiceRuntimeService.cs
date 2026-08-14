using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Models.Voice;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Settings;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class VoiceRuntimeService : IVoiceRuntimeService
{
    private readonly AppSettings _settings;

    private readonly IPresenceRuntimeService _presenceRuntimeService;

    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly ILogger<VoiceRuntimeService> _logger;

    private readonly ConcurrentDictionary<long, string> _nicknames = new();

    private readonly Lock _stateLock = new();

    private VoiceCallPhase _phase = VoiceCallPhase.Idle;

    private Guid? _roomId;

    private long? _incomingFromPlayerId;

    private long? _outgoingToPlayerId;

    private DateTimeOffset? _inviteExpiresAt;

    private DateTimeOffset? _endsAt;

    private List<long> _memberIds = [];

    private string? _statusMessage;

    private bool _canStartCall;

    private bool _doNotDisturb;

    private int _maxParticipants = 4;

    private bool _smallerPlayerIdIsPolite = true;

    private VoiceIceServersResponse? _iceServers;

    public VoiceRuntimeService(
        AppSettings settings,
        IPresenceRuntimeService presenceRuntimeService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<VoiceRuntimeService> logger)
    {
        _settings = settings;
        _presenceRuntimeService = presenceRuntimeService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _doNotDisturb = settings.VoiceDoNotDisturb;

        _presenceRuntimeService.RegisterHandler<VoiceIncomingCallPayload>(VoiceHubEvents.IncomingCall, OnIncomingCall);
        _presenceRuntimeService.RegisterHandler<VoiceCallRejectedPayload>(VoiceHubEvents.CallRejected, OnCallRejected);
        _presenceRuntimeService.RegisterHandler<VoiceCallCanceledPayload>(VoiceHubEvents.CallCanceled, OnCallCanceled);
        _presenceRuntimeService.RegisterHandler<VoicePeerJoinedPayload>(VoiceHubEvents.PeerJoined, OnPeerJoined);
        _presenceRuntimeService.RegisterHandler<VoicePeerLeftPayload>(VoiceHubEvents.PeerLeft, OnPeerLeft);
        _presenceRuntimeService.RegisterHandler<VoiceRoomEndedPayload>(VoiceHubEvents.RoomEnded, OnRoomEnded);
        _presenceRuntimeService.RegisterHandler<VoiceSdpPayload>(VoiceHubEvents.Offer, OnOffer);
        _presenceRuntimeService.RegisterHandler<VoiceSdpPayload>(VoiceHubEvents.Answer, OnAnswer);
        _presenceRuntimeService.RegisterHandler<VoiceIceCandidatePayload>(VoiceHubEvents.IceCandidate, OnIceCandidate);
        _presenceRuntimeService.RegisterHandler<VoiceDoNotDisturbChangedPayload>(VoiceHubEvents.DoNotDisturbChanged, OnDoNotDisturbChanged);
        _presenceRuntimeService.RegisterAfterConnect(OnPresenceConnected);
    }

    public VoiceCallSnapshot Snapshot
    {
        get
        {
            lock (_stateLock)
            {
                return new VoiceCallSnapshot(
                    _phase,
                    _roomId,
                    _incomingFromPlayerId,
                    _outgoingToPlayerId,
                    _inviteExpiresAt,
                    _endsAt,
                    [.._memberIds],
                    _statusMessage,
                    CanInviteMoreLocked());
            }
        }
    }

    public bool CanStartCall
    {
        get
        {
            lock (_stateLock)
                return _canStartCall;
        }
    }

    public bool DoNotDisturb
    {
        get
        {
            lock (_stateLock)
                return _doNotDisturb;
        }
    }

    public int MaxParticipants
    {
        get
        {
            lock (_stateLock)
                return _maxParticipants;
        }
    }

    public bool SmallerPlayerIdIsPolite
    {
        get
        {
            lock (_stateLock)
                return _smallerPlayerIdIsPolite;
        }
    }

    public VoiceIceServersResponse? IceServers
    {
        get
        {
            lock (_stateLock)
                return _iceServers;
        }
    }

    public event EventHandler? StateChanged;

    public event EventHandler<VoiceSdpPayload>? OfferReceived;

    public event EventHandler<VoiceSdpPayload>? AnswerReceived;

    public event EventHandler<VoiceIceCandidatePayload>? IceCandidateReceived;

    public event EventHandler<VoicePeerJoinedPayload>? PeerJoined;

    public event EventHandler<VoicePeerLeftPayload>? PeerLeft;

    public event EventHandler? MediaTeardownRequested;

    public event EventHandler? UnavailableSignaled;

    public void SetPremium(bool isPremium)
    {
        lock (_stateLock)
            _canStartCall = isPremium;

        RaiseStateChanged();
    }

    public void RememberPlayer(long playerId, string nickname)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(nickname))
            return;

        _nicknames[playerId] = nickname.Trim();
    }

    public string GetNickname(long playerId) =>
        _nicknames.GetValueOrDefault(playerId, $"Игрок {playerId}");

    public async Task SetDoNotDisturbAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
            _doNotDisturb = enabled;

        _settings.VoiceDoNotDisturb = enabled;
        AppSettings.Save(_settings);
        RaiseStateChanged();

        try
        {
            await _presenceRuntimeService.InvokeHubAsync("SetDoNotDisturb", [enabled], cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось отправить режим «не беспокоить»");
        }
    }

    public async Task InviteAsync(long targetPlayerId, bool targetOnline = true, CancellationToken cancellationToken = default)
    {
        if (targetPlayerId <= 0)
            return;

        bool canStart;
        lock (_stateLock)
            canStart = _canStartCall;

        if (!canStart)
        {
            SetStatus("Голосовой чат можно начать только с премиум-подпиской.");
            return;
        }

        if (!targetOnline)
        {
            UnavailableSignaled?.Invoke(this, EventArgs.Empty);
            return;
        }

        lock (_stateLock)
        {
            _outgoingToPlayerId = targetPlayerId;
            _statusMessage = null;
            _inviteExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                _iceServers?.InviteTimeoutSeconds > 0 ? _iceServers.InviteTimeoutSeconds : 30);
            if (_phase == VoiceCallPhase.Idle)
                _phase = VoiceCallPhase.Outgoing;
        }

        RaiseStateChanged();

        try
        {
            await _presenceRuntimeService.InvokeHubAsync("Invite", [targetPlayerId], cancellationToken);
        }
        catch (Exception exception)
        {
            ResetToIdle(ResolveHubMessage(exception));
            return;
        }

        await EnsureIceServersAsync(cancellationToken);
    }

    public async Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        Guid roomId;
        lock (_stateLock)
        {
            if (_phase != VoiceCallPhase.Incoming || _roomId is null)
                return;

            roomId = _roomId.Value;
        }

        await EnsureIceServersAsync(cancellationToken);

        try
        {
            await _presenceRuntimeService.InvokeHubAsync("Accept", [roomId], cancellationToken);
        }
        catch (Exception exception)
        {
            SetStatus(ResolveHubMessage(exception));
        }
    }

    public async Task RejectAsync(CancellationToken cancellationToken = default)
    {
        Guid roomId;
        lock (_stateLock)
        {
            if (_roomId is null)
                return;

            roomId = _roomId.Value;
        }

        try
        {
            await _presenceRuntimeService.InvokeHubAsync("Reject", [roomId], cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось отклонить вызов");
        }

        ResetToIdle("Вызов отклонён.");
        MediaTeardownRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task HangupAsync(CancellationToken cancellationToken = default)
    {
        VoiceCallPhase phase;
        Guid? roomId;
        lock (_stateLock)
        {
            phase = _phase;
            roomId = _roomId;
        }

        try
        {
            switch (phase)
            {
                case VoiceCallPhase.Incoming when roomId is not null:
                    await _presenceRuntimeService.InvokeHubAsync("Reject", [roomId.Value], cancellationToken);
                    break;
                case VoiceCallPhase.Outgoing:
                    await _presenceRuntimeService.InvokeHubAsync("Cancel", [roomId], cancellationToken);
                    break;
                case VoiceCallPhase.Active:
                    await _presenceRuntimeService.InvokeHubAsync("Leave", [], cancellationToken);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось завершить голосовой вызов");
        }

        ResetToIdle(null);
        MediaTeardownRequested?.Invoke(this, EventArgs.Empty);
    }

    public Task SendOfferAsync(long targetPlayerId, string sdp, CancellationToken cancellationToken = default) =>
        InvokeSafe("Offer", [targetPlayerId, sdp], cancellationToken);

    public Task SendAnswerAsync(long targetPlayerId, string sdp, CancellationToken cancellationToken = default) =>
        InvokeSafe("Answer", [targetPlayerId, sdp], cancellationToken);

    public Task SendIceCandidateAsync(long targetPlayerId, string candidate, CancellationToken cancellationToken = default) =>
        InvokeSafe("IceCandidate", [targetPlayerId, candidate], cancellationToken);

    private async Task OnPresenceConnected(HubConnection connection, CancellationToken cancellationToken)
    {
        bool doNotDisturb;
        lock (_stateLock)
            doNotDisturb = _doNotDisturb;

        try
        {
            await connection.InvokeAsync("SetDoNotDisturb", doNotDisturb, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось отправить режим «не беспокоить» после подключения");
        }

        await RefreshAccessAsync(cancellationToken);
    }

    private async Task RefreshAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var usageService = scope.ServiceProvider.GetRequiredService<IUsageService>();
            var usage = await usageService.Get();
            SetPremium(usage?.Type is AccessType.FullAccess);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось обновить доступ к голосовому чату");
        }
    }

    private async Task EnsureIceServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IVoiceIceServersClient>();
            var response = await client.GetAsync(cancellationToken);
            if (response is null)
                return;

            lock (_stateLock)
            {
                _iceServers = response;
                _maxParticipants = Math.Max(2, response.MaxParticipants);
                _smallerPlayerIdIsPolite = response.SmallerPlayerIdIsPolite;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось получить ICE-серверы");
        }
    }

    private async Task OnIncomingCall(VoiceIncomingCallPayload payload)
    {
        lock (_stateLock)
        {
            _phase = VoiceCallPhase.Incoming;
            _roomId = payload.RoomId;
            _incomingFromPlayerId = payload.FromPlayerId;
            _inviteExpiresAt = payload.InviteExpiresAt;
            _statusMessage = null;
        }

        RaiseStateChanged();
        await EnsureIceServersAsync(CancellationToken.None);
    }

    private Task OnCallRejected(VoiceCallRejectedPayload payload)
    {
        var reasonText = payload.Reason switch
        {
            "doNotDisturb" => "не беспокоит",
            "declined" => "отклонён",
            "busy" => "занят",
            _ => payload.Reason,
        };

        var nickname = GetNickname(payload.PlayerId);
        var message = $"{nickname}: {reasonText}.";

        bool teardown;
        lock (_stateLock)
        {
            if (_outgoingToPlayerId == payload.PlayerId)
                _outgoingToPlayerId = null;

            if (_phase is VoiceCallPhase.Outgoing)
            {
                teardown = true;
                _phase = VoiceCallPhase.Idle;
                _roomId = null;
                _inviteExpiresAt = null;
                _endsAt = null;
                _memberIds = [];
                _incomingFromPlayerId = null;
                _statusMessage = message;
            }
            else
            {
                teardown = false;
                _statusMessage = message;
            }
        }

        RaiseStateChanged();
        if (teardown)
            MediaTeardownRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task OnCallCanceled(VoiceCallCanceledPayload payload)
    {
        bool teardown;
        lock (_stateLock)
        {
            if (_phase == VoiceCallPhase.Incoming && _roomId == payload.RoomId)
            {
                teardown = true;
                _phase = VoiceCallPhase.Idle;
                _roomId = null;
                _incomingFromPlayerId = null;
                _outgoingToPlayerId = null;
                _inviteExpiresAt = null;
                _endsAt = null;
                _memberIds = [];
                _statusMessage = "Вызов отменён.";
            }
            else
            {
                teardown = false;
            }
        }

        RaiseStateChanged();
        if (teardown)
            MediaTeardownRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task OnPeerJoined(VoicePeerJoinedPayload payload)
    {
        lock (_stateLock)
        {
            _phase = VoiceCallPhase.Active;
            _roomId = payload.RoomId;
            _memberIds = payload.MemberIds.Distinct().ToList();
            _endsAt = payload.EndsAt ?? _endsAt;
            _inviteExpiresAt = null;
            _incomingFromPlayerId = null;
            if (_outgoingToPlayerId is { } outgoing && _memberIds.Contains(outgoing))
                _outgoingToPlayerId = null;

            _statusMessage = null;
        }

        RaiseStateChanged();
        PeerJoined?.Invoke(this, payload);
        return Task.CompletedTask;
    }

    private Task OnPeerLeft(VoicePeerLeftPayload payload)
    {
        lock (_stateLock)
        {
            _memberIds = payload.MemberIds.Distinct().ToList();
        }

        RaiseStateChanged();
        PeerLeft?.Invoke(this, payload);
        return Task.CompletedTask;
    }

    private Task OnRoomEnded(VoiceRoomEndedPayload payload)
    {
        var reason = payload.Reason switch
        {
            "timeout" => "Время разговора истекло.",
            "hostLeft" => "Организатор завершил разговор.",
            "lastPeerLeft" => "Собеседник вышел.",
            "cancelled" => "Вызов отменён.",
            _ => "Разговор завершён.",
        };

        ResetToIdle(reason);
        MediaTeardownRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task OnOffer(VoiceSdpPayload payload)
    {
        OfferReceived?.Invoke(this, payload);
        return Task.CompletedTask;
    }

    private Task OnAnswer(VoiceSdpPayload payload)
    {
        AnswerReceived?.Invoke(this, payload);
        return Task.CompletedTask;
    }

    private Task OnIceCandidate(VoiceIceCandidatePayload payload)
    {
        IceCandidateReceived?.Invoke(this, payload);
        return Task.CompletedTask;
    }

    private Task OnDoNotDisturbChanged(VoiceDoNotDisturbChangedPayload payload)
    {
        lock (_stateLock)
            _doNotDisturb = payload.Enabled;

        _settings.VoiceDoNotDisturb = payload.Enabled;
        AppSettings.Save(_settings);
        RaiseStateChanged();
        return Task.CompletedTask;
    }

    private bool CanInviteMoreLocked()
    {
        if (_phase == VoiceCallPhase.Idle)
            return true;

        if (_outgoingToPlayerId is not null)
            return false;

        return _phase == VoiceCallPhase.Active && _memberIds.Count < _maxParticipants;
    }

    private void ResetToIdle(string? status)
    {
        lock (_stateLock)
        {
            _phase = VoiceCallPhase.Idle;
            _roomId = null;
            _incomingFromPlayerId = null;
            _outgoingToPlayerId = null;
            _inviteExpiresAt = null;
            _endsAt = null;
            _memberIds = [];
            _statusMessage = status;
        }

        RaiseStateChanged();
    }

    private void SetStatus(string message)
    {
        lock (_stateLock)
            _statusMessage = message;

        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private async Task InvokeSafe(string method, object?[] args, CancellationToken cancellationToken)
    {
        try
        {
            await _presenceRuntimeService.InvokeHubAsync(method, args, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось вызвать {Method} голосового хаба", method);
            SetStatus(ResolveHubMessage(exception));
        }
    }

    private static string ResolveHubMessage(Exception exception)
    {
        foreach (var current in EnumerateExceptions(exception))
        {
            var message = current.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message))
                continue;

            const string hubMarker = "HubException:";
            var hubIndex = message.LastIndexOf(hubMarker, StringComparison.OrdinalIgnoreCase);
            if (hubIndex >= 0)
            {
                var detail = message[(hubIndex + hubMarker.Length)..].Trim();
                if (detail.Length > 0)
                    return detail;
            }

            if (!message.Contains("HubException", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("Failed to invoke", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("unexpected error occurred invoking", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }
        }

        return "Не удалось выполнить действие голосового чата.";
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            yield return current;
    }
}
