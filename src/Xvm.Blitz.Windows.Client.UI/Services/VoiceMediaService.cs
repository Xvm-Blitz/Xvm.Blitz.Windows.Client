using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using Xvm.Blitz.Windows.Client.Core.Models.Voice;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.UI.Services;

public sealed class VoiceMediaService : IVoiceMediaService, IAsyncDisposable
{
    private readonly IVoiceRuntimeService _voiceRuntimeService;

    private readonly IAuthorizationService _authorizationService;

    private readonly ILogger<VoiceMediaService> _logger;

    private readonly ConcurrentDictionary<long, PeerSession> _peers = new();

    private readonly ConcurrentDictionary<long, ConcurrentQueue<string>> _earlyIce = new();

    private readonly SemaphoreSlim _sync = new(1, 1);

    private WindowsAudioEndPoint? _capture;

    private AudioEncoder? _encoder;

    private bool _micMuted;

    private string? _mediaError;

    public VoiceMediaService(
        IVoiceRuntimeService voiceRuntimeService,
        IAuthorizationService authorizationService,
        ILogger<VoiceMediaService> logger)
    {
        _voiceRuntimeService = voiceRuntimeService;
        _authorizationService = authorizationService;
        _logger = logger;

        _voiceRuntimeService.PeerJoined += OnPeerJoined;
        _voiceRuntimeService.PeerLeft += OnPeerLeft;
        _voiceRuntimeService.OfferReceived += OnOfferReceived;
        _voiceRuntimeService.AnswerReceived += OnAnswerReceived;
        _voiceRuntimeService.IceCandidateReceived += OnIceCandidateReceived;
        _voiceRuntimeService.MediaTeardownRequested += OnMediaTeardownRequested;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public bool IsMicMuted => _micMuted;

    public string? MediaError => _mediaError;

    public event EventHandler? Changed;

    public void SetMicMuted(bool muted)
    {
        _micMuted = muted;
        ApplyOutgoingAudio();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _voiceRuntimeService.PeerJoined -= OnPeerJoined;
        _voiceRuntimeService.PeerLeft -= OnPeerLeft;
        _voiceRuntimeService.OfferReceived -= OnOfferReceived;
        _voiceRuntimeService.AnswerReceived -= OnAnswerReceived;
        _voiceRuntimeService.IceCandidateReceived -= OnIceCandidateReceived;
        _voiceRuntimeService.MediaTeardownRequested -= OnMediaTeardownRequested;
        await TearDownAllAsync();
        _sync.Dispose();
    }

    private void OnPeerJoined(object? sender, VoicePeerJoinedPayload payload) =>
        _ = Task.Run(() => HandlePeerJoinedAsync(payload));

    private void OnPeerLeft(object? sender, VoicePeerLeftPayload payload) =>
        _ = Task.Run(() => ClosePeerAsync(payload.PlayerId));

    private void OnOfferReceived(object? sender, VoiceSdpPayload payload) =>
        _ = Task.Run(() => HandleRemoteSdpAsync(payload.FromPlayerId, payload.Sdp, RTCSdpType.offer));

    private void OnAnswerReceived(object? sender, VoiceSdpPayload payload) =>
        _ = Task.Run(() => HandleRemoteSdpAsync(payload.FromPlayerId, payload.Sdp, RTCSdpType.answer));

    private void OnIceCandidateReceived(object? sender, VoiceIceCandidatePayload payload) =>
        _ = Task.Run(() => HandleRemoteIceAsync(payload.FromPlayerId, payload.Candidate));

    private void OnMediaTeardownRequested(object? sender, EventArgs e) =>
        _ = Task.Run(TearDownAllAsync);

    private void OnNetworkAddressChanged(object? sender, EventArgs e) =>
        _ = Task.Run(RestartIceAsync);

    private async Task HandlePeerJoinedAsync(VoicePeerJoinedPayload payload)
    {
        var selfId = _authorizationService.TryGetLestaAccountId();
        if (selfId is null or <= 0)
            return;

        IEnumerable<long> remotes = payload.PlayerId == selfId.Value
            ? payload.MemberIds.Where(id => id != selfId.Value)
            : [payload.PlayerId];

        foreach (var remoteId in remotes.Distinct())
        {
            if (remoteId == selfId.Value)
                continue;

            await EnsurePeerAsync(selfId.Value, remoteId);
        }
    }

    private async Task EnsurePeerAsync(long selfPlayerId, long remotePlayerId)
    {
        await _sync.WaitAsync();
        try
        {
            if (_peers.ContainsKey(remotePlayerId))
                return;

            await EnsureCaptureAsync();
            if (_capture is null || _encoder is null)
                return;

            var playback = new WindowsAudioEndPoint(_encoder, disableSource: true);
            playback.RestrictFormats(IsOpusMono);
            await playback.StartAudioSink();

            var iceServers = MapIceServers(_voiceRuntimeService.IceServers);
            var peerConnection = new RTCPeerConnection(new RTCConfiguration { iceServers = iceServers });
            var audioTrack = new MediaStreamTrack(_capture.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);
            peerConnection.addTrack(audioTrack);

            var session = new PeerSession(remotePlayerId, peerConnection, playback, audioTrack)
            {
                Polite = _voiceRuntimeService.SmallerPlayerIdIsPolite
                    ? selfPlayerId < remotePlayerId
                    : selfPlayerId > remotePlayerId,
            };

            _capture.OnAudioSourceEncodedSample += session.SendAudio;
            peerConnection.OnAudioFormatsNegotiated += formats =>
            {
                var formatList = formats.ToList();
                var format = formatList.FirstOrDefault(IsOpusMono);
                if (format.Codec != AudioCodecsEnum.OPUS)
                    format = formatList.FirstOrDefault();
                if (format.Codec == AudioCodecsEnum.Unknown)
                    return;

                _capture.SetAudioSourceFormat(format);
                playback.SetAudioSinkFormat(format);
            };
            peerConnection.OnRtpPacketReceived += (_, media, packet) =>
            {
                if (media != SDPMediaTypesEnum.audio)
                    return;

#pragma warning disable CS0618
                playback.GotAudioRtp(
                    null,
                    packet.Header.SyncSource,
                    packet.Header.SequenceNumber,
                    packet.Header.Timestamp,
                    packet.Header.PayloadType,
                    packet.Header.MarkerBit != 0,
                    packet.Payload);
#pragma warning restore CS0618
            };
            peerConnection.onicecandidate += candidate =>
            {
                if (candidate is null || string.IsNullOrWhiteSpace(candidate.candidate))
                    return;

                _ = _voiceRuntimeService.SendIceCandidateAsync(remotePlayerId, NormalizeIceCandidate(candidate.candidate));
            };
            peerConnection.onnegotiationneeded += () =>
            {
                if (session.Polite)
                    return;

                _ = CreateAndSendOfferAsync(session);
            };
            peerConnection.onconnectionstatechange += state =>
            {
                _logger.LogInformation("WebRTC с игроком {PlayerId}: {State}", remotePlayerId, state);
                if (state == RTCPeerConnectionState.failed)
                    SetMediaError("Не удалось установить голосовое соединение.");
            };

            _peers[remotePlayerId] = session;
            ApplyOutgoingAudio();
            DrainPendingIce(session);
        }
        catch (Exception exception)
        {
            SetMediaError("Не удалось создать голосовое соединение.");
            _logger.LogError(exception, "Ошибка создания PeerConnection для {PlayerId}", remotePlayerId);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task CreateAndSendOfferAsync(PeerSession session)
    {
        string? sdp = null;
        await _sync.WaitAsync();
        try
        {
            if (session.Polite || session.MakingOffer)
                return;

            if (session.PeerConnection.signalingState != RTCSignalingState.stable)
                return;

            session.MakingOffer = true;
            var offer = session.PeerConnection.createOffer();
            await session.PeerConnection.setLocalDescription(offer);
            sdp = session.PeerConnection.localDescription.sdp.ToString();
        }
        catch (Exception exception)
        {
            session.MakingOffer = false;
            _logger.LogWarning(exception, "Не удалось создать offer игроку {PlayerId}", session.PlayerId);
            return;
        }
        finally
        {
            _sync.Release();
        }

        try
        {
            if (sdp is not null)
                await _voiceRuntimeService.SendOfferAsync(session.PlayerId, sdp);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось отправить offer игроку {PlayerId}", session.PlayerId);
        }
        finally
        {
            await _sync.WaitAsync();
            try
            {
                session.MakingOffer = false;
            }
            finally
            {
                _sync.Release();
            }
        }
    }

    private async Task HandleRemoteSdpAsync(long fromPlayerId, string sdp, RTCSdpType type)
    {
        if (string.IsNullOrWhiteSpace(sdp))
            return;

        var selfId = _authorizationService.TryGetLestaAccountId();
        if (selfId is null)
            return;

        await EnsurePeerAsync(selfId.Value, fromPlayerId);

        string? answerSdp = null;
        await _sync.WaitAsync();
        try
        {
            if (!_peers.TryGetValue(fromPlayerId, out var session))
                return;

            if (type == RTCSdpType.offer)
            {
                if (!session.Polite &&
                    (session.MakingOffer || session.PeerConnection.signalingState != RTCSignalingState.stable))
                {
                    _logger.LogInformation("Игнор offer от {PlayerId}: мы инициатор", fromPlayerId);
                    return;
                }

                if (session.PeerConnection.signalingState != RTCSignalingState.stable)
                {
                    _logger.LogWarning(
                        "Нельзя принять offer от {PlayerId} в состоянии {State}",
                        fromPlayerId,
                        session.PeerConnection.signalingState);
                    return;
                }

                var remoteOffer = session.PeerConnection.setRemoteDescription(
                    new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
                if (remoteOffer != SetDescriptionResultEnum.OK)
                {
                    _logger.LogWarning("setRemoteDescription(offer) для {PlayerId}: {Result}", fromPlayerId, remoteOffer);
                    return;
                }

                session.RemoteDescriptionSet = true;
                DrainPendingIce(session);

                var answer = session.PeerConnection.createAnswer();
                await session.PeerConnection.setLocalDescription(answer);
                answerSdp = session.PeerConnection.localDescription.sdp.ToString();
            }
            else
            {
                var remoteAnswer = session.PeerConnection.setRemoteDescription(
                    new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
                if (remoteAnswer != SetDescriptionResultEnum.OK)
                {
                    _logger.LogWarning("setRemoteDescription(answer) для {PlayerId}: {Result}", fromPlayerId, remoteAnswer);
                    return;
                }

                session.RemoteDescriptionSet = true;
                DrainPendingIce(session);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ошибка обработки SDP от {PlayerId}", fromPlayerId);
        }
        finally
        {
            _sync.Release();
        }

        if (answerSdp is not null)
        {
            try
            {
                await _voiceRuntimeService.SendAnswerAsync(fromPlayerId, answerSdp);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Не удалось отправить answer игроку {PlayerId}", fromPlayerId);
            }
        }
    }

    private async Task HandleRemoteIceAsync(long fromPlayerId, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        var selfId = _authorizationService.TryGetLestaAccountId();
        if (selfId is not null)
            await EnsurePeerAsync(selfId.Value, fromPlayerId);

        await _sync.WaitAsync();
        try
        {
            if (!_peers.TryGetValue(fromPlayerId, out var session))
            {
                _earlyIce.GetOrAdd(fromPlayerId, _ => new ConcurrentQueue<string>()).Enqueue(candidate);
                return;
            }

            if (!session.RemoteDescriptionSet)
            {
                session.PendingIce.Add(candidate);
                return;
            }

            AddIceCandidate(session, candidate);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ошибка ICE candidate от {PlayerId}", fromPlayerId);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task ClosePeerAsync(long playerId)
    {
        await _sync.WaitAsync();
        try
        {
            if (_peers.TryRemove(playerId, out var session))
                await DisposePeerAsync(session);

            if (_peers.IsEmpty)
                await StopCaptureAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task TearDownAllAsync()
    {
        await _sync.WaitAsync();
        try
        {
            foreach (var session in _peers.Values)
                await DisposePeerAsync(session);

            _peers.Clear();
            _earlyIce.Clear();
            await StopCaptureAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task RestartIceAsync()
    {
        List<PeerSession> sessions;
        await _sync.WaitAsync();
        try
        {
            sessions = _peers.Values.Where(session => !session.Polite).ToList();
        }
        finally
        {
            _sync.Release();
        }

        foreach (var session in sessions)
            await CreateAndSendOfferAsync(session);
    }

    private void DrainPendingIce(PeerSession session)
    {
        if (_earlyIce.TryRemove(session.PlayerId, out var early))
        {
            while (early.TryDequeue(out var candidate))
                session.PendingIce.Add(candidate);
        }

        if (!session.RemoteDescriptionSet || session.PendingIce.Count == 0)
            return;

        foreach (var candidate in session.PendingIce)
            AddIceCandidate(session, candidate);

        session.PendingIce.Clear();
    }

    private void AddIceCandidate(PeerSession session, string candidate)
    {
        try
        {
            session.PeerConnection.addIceCandidate(
                new RTCIceCandidateInit
                {
                    candidate = UnwrapIceCandidate(candidate),
                    sdpMid = "0",
                    sdpMLineIndex = 0,
                });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось добавить ICE candidate от {PlayerId}", session.PlayerId);
        }
    }

    private async Task EnsureCaptureAsync()
    {
        if (_capture is not null)
            return;

        try
        {
            _encoder = new AudioEncoder();
            _capture = new WindowsAudioEndPoint(_encoder, disableSink: true);
            _capture.RestrictFormats(IsOpusMono);
            _capture.OnAudioSourceError += message =>
            {
                SetMediaError("Нет доступа к микрофону. Разрешите микрофон для XVM Blitz в параметрах Windows.");
                _logger.LogWarning("Ошибка микрофона: {Message}", message);
            };
            await _capture.StartAudio();
            _mediaError = null;
        }
        catch (Exception exception)
        {
            SetMediaError("Нет доступа к микрофону. Разрешите микрофон для XVM Blitz в параметрах Windows.");
            _logger.LogError(exception, "Не удалось запустить захват микрофона");
            _capture = null;
        }
    }

    private async Task StopCaptureAsync()
    {
        if (_capture is null)
            return;

        try
        {
            await _capture.CloseAudio();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ошибка остановки микрофона");
        }

        _capture = null;
        _encoder = null;
    }

    private async Task DisposePeerAsync(PeerSession session)
    {
        if (_capture is not null)
            _capture.OnAudioSourceEncodedSample -= session.SendAudio;

        try
        {
            session.PeerConnection.Close("hangup");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ошибка закрытия PeerConnection {PlayerId}", session.PlayerId);
        }

        try
        {
            await session.Playback.CloseAudio();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ошибка закрытия воспроизведения {PlayerId}", session.PlayerId);
        }
    }

    private void ApplyOutgoingAudio()
    {
        var enabled = !_micMuted;
        foreach (var session in _peers.Values)
            session.OutgoingEnabled = enabled;
    }

    private void SetMediaError(string message)
    {
        _mediaError = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsOpusMono(AudioFormat format) =>
        format.Codec == AudioCodecsEnum.OPUS && format.ChannelCount <= 2;

    private static string NormalizeIceCandidate(string candidate)
    {
        var trimmed = candidate.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed.StartsWith('{') ||
            trimmed.StartsWith("candidate:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("a=candidate:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return "candidate:" + trimmed;
    }

    private static string UnwrapIceCandidate(string candidate)
    {
        var trimmed = candidate.Trim();
        if (!trimmed.StartsWith('{'))
            return trimmed;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.TryGetProperty("candidate", out var property))
            {
                var inner = property.GetString();
                if (!string.IsNullOrWhiteSpace(inner))
                    return inner;
            }
        }
        catch (JsonException)
        {
        }

        return trimmed;
    }

    private static List<RTCIceServer> MapIceServers(VoiceIceServersResponse? response)
    {
        if (response?.IceServers is null || response.IceServers.Count == 0)
        {
            return
            [
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
            ];
        }

        return response.IceServers
            .SelectMany(server => server.Urls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => new RTCIceServer
                {
                    urls = url.Trim(),
                    username = server.Username,
                    credential = server.Credential,
                }))
            .DefaultIfEmpty(new RTCIceServer { urls = "stun:stun.l.google.com:19302" })
            .ToList();
    }

    private sealed class PeerSession(
        long playerId,
        RTCPeerConnection peerConnection,
        WindowsAudioEndPoint playback,
        MediaStreamTrack audioTrack)
    {
        public long PlayerId { get; } = playerId;

        public RTCPeerConnection PeerConnection { get; } = peerConnection;

        public WindowsAudioEndPoint Playback { get; } = playback;

        public MediaStreamTrack AudioTrack { get; } = audioTrack;

        public bool MakingOffer { get; set; }

        public bool Polite { get; set; }

        public bool OutgoingEnabled { get; set; }

        public bool RemoteDescriptionSet { get; set; }

        public List<string> PendingIce { get; } = [];

        public void SendAudio(uint durationRtpUnits, byte[] sample)
        {
            if (!OutgoingEnabled)
                return;

            PeerConnection.SendAudio(durationRtpUnits, sample);
        }
    }
}
