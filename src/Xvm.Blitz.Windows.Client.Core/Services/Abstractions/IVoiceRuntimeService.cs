using Xvm.Blitz.Windows.Client.Core.Models.Voice;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IVoiceRuntimeService
{
    VoiceCallSnapshot Snapshot { get; }

    bool CanStartCall { get; }

    bool DoNotDisturb { get; }

    int MaxParticipants { get; }

    bool SmallerPlayerIdIsPolite { get; }

    VoiceIceServersResponse? IceServers { get; }

    event EventHandler? StateChanged;

    event EventHandler<VoiceSdpPayload>? OfferReceived;

    event EventHandler<VoiceSdpPayload>? AnswerReceived;

    event EventHandler<VoiceIceCandidatePayload>? IceCandidateReceived;

    event EventHandler<VoicePeerJoinedPayload>? PeerJoined;

    event EventHandler<VoicePeerLeftPayload>? PeerLeft;

    event EventHandler? MediaTeardownRequested;

    event EventHandler? UnavailableSignaled;

    void SetPremium(bool isPremium);

    void RememberPlayer(long playerId, string nickname);

    string GetNickname(long playerId);

    Task SetDoNotDisturbAsync(bool enabled, CancellationToken cancellationToken = default);

    Task InviteAsync(long targetPlayerId, bool targetOnline = true, CancellationToken cancellationToken = default);

    Task AcceptAsync(CancellationToken cancellationToken = default);

    Task RejectAsync(CancellationToken cancellationToken = default);

    Task HangupAsync(CancellationToken cancellationToken = default);

    Task SendOfferAsync(long targetPlayerId, string sdp, CancellationToken cancellationToken = default);

    Task SendAnswerAsync(long targetPlayerId, string sdp, CancellationToken cancellationToken = default);

    Task SendIceCandidateAsync(long targetPlayerId, string candidate, CancellationToken cancellationToken = default);
}
