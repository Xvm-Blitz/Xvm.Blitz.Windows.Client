namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IVoiceMediaService
{
    bool IsMicMuted { get; }

    string? MediaError { get; }

    event EventHandler? Changed;

    void SetMicMuted(bool muted);
}
