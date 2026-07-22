namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public sealed class SessionListItem(Guid id, DateTimeOffset createdAt, DateTimeOffset? endedAt)
{
    public Guid Id { get; } = id;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset? EndedAt { get; } = endedAt;

    public bool IsActive => EndedAt is null;

    public string DisplayText
    {
        get
        {
            var created = CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            if (IsActive)
                return $"Активная · {created}";

            var ended = EndedAt!.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            return $"Завершена · {created} — {ended}";
        }
    }

    public override string ToString() => DisplayText;
}
