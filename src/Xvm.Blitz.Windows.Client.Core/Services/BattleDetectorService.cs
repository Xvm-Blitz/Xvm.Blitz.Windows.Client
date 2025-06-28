using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Channels;
using Aspose.Imaging.FileFormats.Png;
using Aspose.Imaging.ImageOptions;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.WindowsApis;
using Image = Aspose.Imaging.Image;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class PacketCaptureSettings
{
    public required string ReplayPath { get; init; }

    public required string AppName { get; init; }
}

public sealed class BattleDetectorService(
    PacketCaptureSettings settings,
    IStatisticsClient statisticsClient,
    Func<BattleStatistics, Task> onBattleStartedReceived,
    Func<Task> onBattleEndedReceived,
    ILogger<BattleDetectorService> logger) : IBattleDetectorService, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public void StartDetect()
    {
        var replayPath = settings.ReplayPath;
        if (string.IsNullOrEmpty(replayPath))
            return;

        _ = Task.Run(
            async () =>
            {
                while (!_cts.IsCancellationRequested)
                    try
                    {
                        using var fileWatcher = new FileSystemWatcher(replayPath);

                        var createdResult = fileWatcher.WaitForChanged(WatcherChangeTypes.Created);
                        if (Path.GetFileName(Directory.GetDirectories(settings.ReplayPath).SingleOrDefault()) != createdResult.Name)
                            continue;

                        var screenshotsChannel = Channel.CreateUnbounded<byte[]>();
                        var screenshotCreatingTask = Task.Run(
                            async () =>
                            {
                                for (var delay = 1d; delay <= 2; delay += 0.5)
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(delay));
                                    var screenshot = await CaptureFullScreenWithWindowHighlight(settings.AppName);
                                    if (screenshot is null)
                                        return;

                                    using var memoryStream = new MemoryStream(screenshot);
                                    using var image = Image.Load(memoryStream);
                                    using var compressedScreenshot = new MemoryStream();
                                    var options = new PngOptions
                                    {
                                        PngCompressionLevel = PngCompressionLevel.DeflateRecomended,
                                        ColorType = PngColorType.Grayscale,
                                        KeepMetadata = false
                                    };

                                    image.Save(compressedScreenshot, options);
                                    await screenshotsChannel.Writer.WriteAsync(compressedScreenshot.ToArray(), _cts.Token);
                                }
                            });

                        var screenshotRecognizeTask = Task.Run(
                            async () =>
                            {
                                while (await screenshotsChannel.Reader.WaitToReadAsync(_cts.Token))
                                {
                                    var screenshot = await screenshotsChannel.Reader.ReadAsync(_cts.Token);
                                    var battle = await statisticsClient.GetBattleStatistics(screenshot);
                                    if (battle is null)
                                        continue;

                                    logger.LogInformation(
                                        "Получены данные боя: союзников {AlliesCount}, противников {EnemiesCount}",
                                        battle.Allies.Count,
                                        battle.Enemies.Count);

                                    await onBattleStartedReceived(battle);
                                    break;
                                }
                            });

                        await screenshotCreatingTask;
                        screenshotsChannel.Writer.Complete();
                        await screenshotRecognizeTask;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Ошибка обработки начала боя");
                    }
            });

        _ = Task.Run(
            async () =>
            {
                while (!_cts.IsCancellationRequested)
                    try
                    {
                        using var fileWatcher = new FileSystemWatcher(replayPath);

                        fileWatcher.WaitForChanged(WatcherChangeTypes.Deleted);
                        if (Directory.GetDirectories(settings.ReplayPath).SingleOrDefault() is not null)
                            continue;

                        await onBattleEndedReceived();
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Ошибка обработки конца боя");
                    }
            });
    }

    public Task StopDetect()
    {
        return _cts.CancelAsync();
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    private static async Task<byte[]?> CaptureFullScreenWithWindowHighlight(string processName)
    {
        var tanksProcess = Process.GetProcessesByName(processName).SingleOrDefault();
        if (tanksProcess is null)
            return null;

        var windowRectangle = new WindowRectangle();
        WindowsApi.GetWindowRect(tanksProcess.MainWindowHandle, ref windowRectangle);

        var width = windowRectangle.Right - windowRectangle.Left;
        var height = windowRectangle.Bottom - windowRectangle.Top;

        await WindowsApi.ForceSetForegroundWindow(tanksProcess.MainWindowHandle);

#pragma warning disable CA1416
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(new Point(windowRectangle.Left, windowRectangle.Top), Point.Empty, new Size(width, height));

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Jpeg);
#pragma warning restore CA1416

        return memoryStream.ToArray();
    }
}
