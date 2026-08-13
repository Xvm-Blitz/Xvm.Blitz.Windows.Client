using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Models.Voice;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.UI.Services;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public sealed class VoiceOverlayViewModel : ReactiveObject, IDisposable
{
    private readonly IVoiceRuntimeService _voiceRuntimeService;

    private readonly IVoiceMediaService _voiceMediaService;

    private readonly DispatcherTimer _timer;

    private readonly VoiceCallTonePlayer _tones = new();

    private VoiceCallPhase _tonePhase = VoiceCallPhase.Idle;

    private string? _toneStatus;

    private string _title = "Голосовой чат";

    private string _participantsText = string.Empty;

    private string _countdownText = string.Empty;

    private string? _statusText;

    private bool _hasParticipantsText;

    private bool _hasStatusText;

    private bool _isIncomingVisible;

    private bool _isInCallVisible;

    private bool _isHangupVisible;

    private bool _isOverlayVisible;

    private bool _isMicMuted;

    private bool _doNotDisturb;

    private string _muteButtonText = "Микрофон";

    public VoiceOverlayViewModel(IVoiceRuntimeService voiceRuntimeService, IVoiceMediaService voiceMediaService)
    {
        _voiceRuntimeService = voiceRuntimeService;
        _voiceMediaService = voiceMediaService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshFromState();
        _timer.Start();

        _voiceRuntimeService.StateChanged += OnStateChanged;
        _voiceMediaService.Changed += OnMediaChanged;
        _voiceRuntimeService.UnavailableSignaled += OnUnavailableSignaled;
        RefreshFromState();
    }

    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string ParticipantsText
    {
        get => _participantsText;
        private set
        {
            this.RaiseAndSetIfChanged(ref _participantsText, value);
            HasParticipantsText = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool HasParticipantsText
    {
        get => _hasParticipantsText;
        private set => this.RaiseAndSetIfChanged(ref _hasParticipantsText, value);
    }

    public string CountdownText
    {
        get => _countdownText;
        private set => this.RaiseAndSetIfChanged(ref _countdownText, value);
    }

    public string? StatusText
    {
        get => _statusText;
        private set
        {
            this.RaiseAndSetIfChanged(ref _statusText, value);
            HasStatusText = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool HasStatusText
    {
        get => _hasStatusText;
        private set => this.RaiseAndSetIfChanged(ref _hasStatusText, value);
    }

    public bool IsIncomingVisible
    {
        get => _isIncomingVisible;
        private set => this.RaiseAndSetIfChanged(ref _isIncomingVisible, value);
    }

    public bool IsInCallVisible
    {
        get => _isInCallVisible;
        private set => this.RaiseAndSetIfChanged(ref _isInCallVisible, value);
    }

    public bool IsHangupVisible
    {
        get => _isHangupVisible;
        private set => this.RaiseAndSetIfChanged(ref _isHangupVisible, value);
    }

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        private set => this.RaiseAndSetIfChanged(ref _isOverlayVisible, value);
    }

    public bool IsMicMuted
    {
        get => _isMicMuted;
        private set => this.RaiseAndSetIfChanged(ref _isMicMuted, value);
    }

    public bool DoNotDisturb
    {
        get => _doNotDisturb;
        set
        {
            if (!this.RaiseAndSetIfChanged(ref _doNotDisturb, value))
                return;

            _ = _voiceRuntimeService.SetDoNotDisturbAsync(value);
        }
    }

    public string MuteButtonText
    {
        get => _muteButtonText;
        private set => this.RaiseAndSetIfChanged(ref _muteButtonText, value);
    }

    public ObservableCollection<string> ParticipantNames { get; } = [];

    public void Accept() => _ = _voiceRuntimeService.AcceptAsync();

    public void Reject() => _ = _voiceRuntimeService.RejectAsync();

    public void Hangup() => _ = _voiceRuntimeService.HangupAsync();

    public void ToggleMute() => _voiceMediaService.SetMicMuted(!_voiceMediaService.IsMicMuted);

    public void Dispose()
    {
        _timer.Stop();
        _tones.Dispose();
        _voiceRuntimeService.StateChanged -= OnStateChanged;
        _voiceMediaService.Changed -= OnMediaChanged;
        _voiceRuntimeService.UnavailableSignaled -= OnUnavailableSignaled;
    }

    private void OnStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RefreshFromState);

    private void OnMediaChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RefreshFromState);

    private void OnUnavailableSignaled(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(_tones.PlayBusy);

    private void RefreshFromState()
    {
        var snapshot = _voiceRuntimeService.Snapshot;
        IsMicMuted = _voiceMediaService.IsMicMuted;
        MuteButtonText = IsMicMuted ? "Микрофон выкл" : "Микрофон вкл";
        if (_doNotDisturb != _voiceRuntimeService.DoNotDisturb)
        {
            _doNotDisturb = _voiceRuntimeService.DoNotDisturb;
            this.RaisePropertyChanged(nameof(DoNotDisturb));
        }

        var mediaError = _voiceMediaService.MediaError;
        StatusText = string.IsNullOrWhiteSpace(mediaError) ? snapshot.StatusMessage : mediaError;

        IsIncomingVisible = snapshot.Phase == VoiceCallPhase.Incoming;
        IsInCallVisible = snapshot.Phase is VoiceCallPhase.Outgoing or VoiceCallPhase.Active;
        IsHangupVisible = snapshot.Phase is VoiceCallPhase.Outgoing or VoiceCallPhase.Active;
        var visible = snapshot.Phase != VoiceCallPhase.Idle;
        if (IsOverlayVisible != visible)
        {
            IsOverlayVisible = visible;
            App.ApplyVoiceOverlayVisibility(visible);
        }

        Title = snapshot.Phase switch
        {
            VoiceCallPhase.Incoming => "Входящий вызов",
            VoiceCallPhase.Outgoing => "Исходящий вызов",
            VoiceCallPhase.Active => "Голосовой чат",
            _ => "Голосовой чат",
        };

        var names = new List<string>();
        if (snapshot.Phase == VoiceCallPhase.Incoming && snapshot.IncomingFromPlayerId is { } incoming)
            names.Add(_voiceRuntimeService.GetNickname(incoming));
        else if (snapshot.Phase == VoiceCallPhase.Outgoing && snapshot.OutgoingToPlayerId is { } outgoing)
            names.Add(_voiceRuntimeService.GetNickname(outgoing));
        else
            names.AddRange(
                snapshot.MemberIds
                    .Select(_voiceRuntimeService.GetNickname)
                    .Distinct());

        if (snapshot.OutgoingToPlayerId is { } waiting && snapshot.Phase == VoiceCallPhase.Active)
            names.Add($"{_voiceRuntimeService.GetNickname(waiting)} (ожидание)");

        ParticipantsText = names.Count == 0 ? string.Empty : string.Join(", ", names);

        ParticipantNames.Clear();
        foreach (var name in names)
            ParticipantNames.Add(name);

        CountdownText = FormatCountdown(snapshot);
        SyncTones(snapshot);
        App.ApplyVoiceOverlayVisibility(IsOverlayVisible);
    }

    private void SyncTones(VoiceCallSnapshot snapshot)
    {
        if (snapshot.Phase == _tonePhase && snapshot.StatusMessage == _toneStatus)
            return;

        var previous = _tonePhase;
        _tonePhase = snapshot.Phase;
        _toneStatus = snapshot.StatusMessage;

        switch (snapshot.Phase)
        {
            case VoiceCallPhase.Incoming:
                _tones.PlayIncoming();
                break;
            case VoiceCallPhase.Outgoing:
                _tones.PlayRingback();
                break;
            case VoiceCallPhase.Active:
                _tones.Stop();
                break;
            default:
                if ((previous == VoiceCallPhase.Outgoing && !string.IsNullOrWhiteSpace(snapshot.StatusMessage)) ||
                    (previous == VoiceCallPhase.Idle && IsUnavailableStatus(snapshot.StatusMessage)))
                    _tones.PlayBusy();
                else
                    _tones.Stop();
                break;
        }
    }

    private static bool IsUnavailableStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        (status.Contains("занят", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("не беспокоит", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("отклон", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("отменён", StringComparison.OrdinalIgnoreCase));

    private static string FormatCountdown(VoiceCallSnapshot snapshot)
    {
        var deadline = snapshot.Phase is VoiceCallPhase.Incoming or VoiceCallPhase.Outgoing
            ? snapshot.InviteExpiresAt
            : snapshot.EndsAt;

        if (deadline is null)
            return snapshot.Phase == VoiceCallPhase.Outgoing ? "ожидание ответа" : string.Empty;

        var remaining = deadline.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return "00:00";

        return $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }
}
