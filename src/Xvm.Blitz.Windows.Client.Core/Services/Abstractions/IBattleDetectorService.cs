namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IBattleDetectorService
{
    void StartDetect();

    Task StopDetect();
}