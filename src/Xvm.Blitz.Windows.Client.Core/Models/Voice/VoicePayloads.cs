namespace Xvm.Blitz.Windows.Client.Core.Models.Voice;

public sealed record VoiceIncomingCallPayload(Guid RoomId, long FromPlayerId, DateTimeOffset InviteExpiresAt);

public sealed record VoiceCallRejectedPayload(long PlayerId, string Reason);

public sealed record VoiceCallCanceledPayload(Guid RoomId, long PlayerId);

public sealed record VoicePeerJoinedPayload(Guid RoomId, long PlayerId, IReadOnlyCollection<long> MemberIds, DateTimeOffset? EndsAt);

public sealed record VoicePeerLeftPayload(Guid RoomId, long PlayerId, IReadOnlyCollection<long> MemberIds);

public sealed record VoiceRoomEndedPayload(Guid RoomId, string Reason);

public sealed record VoiceSdpPayload(Guid RoomId, long FromPlayerId, string Sdp);

public sealed record VoiceIceCandidatePayload(Guid RoomId, long FromPlayerId, string Candidate);

public sealed record VoiceDoNotDisturbChangedPayload(bool Enabled);
