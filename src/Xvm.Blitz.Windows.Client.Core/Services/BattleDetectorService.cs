using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models.Battles;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.WindowsApis;

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
    ILogger<BattleDetectorService> logger,
    Func<Task>? onLoadingScreenRequired = null) : IBattleDetectorService, IDisposable
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

                        if (!LoadingScreenPatch.IsReplaced)
                        {
                            logger.LogWarning(
                                "Battle detected, but loading screen is not replaced. Recognition skipped.");

                            if (onLoadingScreenRequired is not null)
                                await onLoadingScreenRequired();

                            continue;
                        }

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
                                    using var compressedScreenshot = new MemoryStream();
#pragma warning disable CA1416
                                    using var image = Image.FromStream(memoryStream);

                                    var grayscaleImage = ConvertToGrayscale(image);

                                    grayscaleImage.Save(compressedScreenshot, ImageFormat.Jpeg);
                                    compressedScreenshot.Seek(0,  SeekOrigin.Begin);

                                    await screenshotsChannel.Writer.WriteAsync(compressedScreenshot.ToArray(), _cts.Token);
#pragma warning restore CA1416
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
                                        "Battle data received: {AlliesCount} allies, {EnemiesCount} enemies",
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
                        logger.LogError(exception, "Error processing battle start");
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
                        logger.LogError(exception, "Error processing battle end");
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

    public static Image ConvertToGrayscale(Image image)
    {
#pragma warning disable CA1416
        Image grayscaleImage = new Bitmap(image.Width, image.Height, image.PixelFormat);

        var attributes = new ImageAttributes();
        var grayscaleMatrix = new ColorMatrix(
            new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

        attributes.SetColorMatrix(grayscaleMatrix);

        using var g = Graphics.FromImage(grayscaleImage);

        g.DrawImage(
            image,
            new Rectangle(
                0,
                0,
                grayscaleImage.Width,
                grayscaleImage.Height),
            0,
            0,
            grayscaleImage.Width,
            grayscaleImage.Height,
            GraphicsUnit.Pixel,
            attributes);
#pragma warning restore CA1416

        return grayscaleImage;
    }
}
