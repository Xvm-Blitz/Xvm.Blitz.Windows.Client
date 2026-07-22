namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public enum TutorialIllustration
{
    Welcome,
    Authorization,
    BattleSessions,
    SecretKey,
    LoadingScreen,
    Replays,
    Overlays,
    HidePanels,
    Tray,
    Updates,
    BattleFlow,
    Finish
}

public sealed class TutorialStep
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Tip { get; init; }

    public required TutorialIllustration Illustration { get; init; }

    public required IReadOnlyList<string> Highlights { get; init; }
}
