using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class StatisticsErrorOverlayWindow : Window
{
    private static StatisticsErrorOverlayWindow? _current;

    public StatisticsErrorOverlayWindow()
    {
        AvaloniaXamlLoader.Load(this);
        OverlayWindowChrome.ExcludeFromAltTab(this);
    }

    public static Task ShowAsync(string message)
    {
        var completionSource = new TaskCompletionSource();
        Dispatcher.UIThread.Post(
            async () =>
            {
                try
                {
                    _current?.Close();
                    var window = new StatisticsErrorOverlayWindow();
                    _current = window;
                    await window.ShowErrorAsync(message);
                    if (ReferenceEquals(_current, window))
                        _current = null;

                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

        return completionSource.Task;
    }

    private async Task ShowErrorAsync(string message)
    {
        if (this.FindControl<TextBlock>("ErrorMessage") is { } errorMessage)
            errorMessage.Text = FormatMessage(message);

        Show();
        var hideTask = Task.Delay(TimeSpan.FromSeconds(5));
        await ShakeAsync();
        await hideTask;

        if (IsVisible)
            Close();
    }

    private static string FormatMessage(string message) =>
        message.Replace(". ", ".\n", StringComparison.Ordinal);

    private async Task ShakeAsync()
    {
        if (this.FindControl<Border>("Root") is not { } root)
            return;

        var transform = new TranslateTransform();
        root.RenderTransform = transform;

        const double amplitude = 12;
        for (var step = 0; step < 12; step++)
        {
            var direction = step % 2 == 0 ? 1 : -1;
            transform.X = direction * amplitude * (1 - step / 12d);
            await Task.Delay(28);
        }

        transform.X = 0;
    }
}
