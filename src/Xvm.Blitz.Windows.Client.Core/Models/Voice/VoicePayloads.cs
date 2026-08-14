namespace Xvm.Blitz.Windows.Client.Core.Models.Voice;

public sealed record VoiceNicknameEntry(long PlayerId, string Nickname);

public sealed record VoiceIncomingCallPayload(
    Guid RoomId,
    long FromPlayerId,
    DateTimeOffset InviteExpiresAt,
    string? FromNickname = null);

public sealed record VoiceCallRejectedPayload(long PlayerId, string Reason, string? Nickname = null);

public sealed record VoiceCallCanceledPayload(Guid RoomId, long PlayerId, string? Nickname = null);

public sealed record VoicePeerJoinedPayload(
    Guid RoomId,
    long PlayerId,
    IReadOnlyCollection<long> MemberIds,
    DateTimeOffset? EndsAt,
    IReadOnlyCollection<VoiceNicknameEntry>? Nicknames = null);

public sealed record VoicePeerLeftPayload(Guid RoomId, long PlayerId, IReadOnlyCollection<long> MemberIds);

public sealed record VoiceRoomEndedPayload(Guid RoomId, string Reason);

public sealed record VoiceSdpPayload(Guid RoomId, long FromPlayerId, string Sdp);

public sealed record VoiceIceCandidatePayload(Guid RoomId, long FromPlayerId, string Candidate);

public sealed record VoiceDoNotDisturbChangedPayload(bool Enabled);
