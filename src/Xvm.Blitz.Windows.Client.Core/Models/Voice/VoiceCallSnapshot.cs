namespace Xvm.Blitz.Windows.Client.Core.Models.Voice;

public enum VoiceCallPhase
{
    Idle,
    Incoming,
    Outgoing,
    Active,
}

public sealed record VoiceCallSnapshot(
    VoiceCallPhase Phase,
    Guid? RoomId,
    long? IncomingFromPlayerId,
    long? OutgoingToPlayerId,
    DateTimeOffset? InviteExpiresAt,
    DateTimeOffset? EndsAt,
    IReadOnlyList<long> MemberIds,
    string? StatusMessage,
    bool CanInviteMore);
