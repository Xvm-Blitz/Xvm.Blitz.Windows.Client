using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public static class AppNotification
{
    private static WindowNotificationManager? _notificationManager;

    public static Task ShowError(string title, string message) =>
        Show(title, message, NotificationType.Error);

    public static Task ShowWarning(string title, string message) =>
        Show(title, message, NotificationType.Warning);

    private static Task Show(string title, string message, NotificationType type)
    {
        var completionSource = new TaskCompletionSource();
        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    ShowInternal(title, message, type);
                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

        return completionSource.Task;
    }

    private static void ShowInternal(string title, string message, NotificationType type)
    {
        if (App.MainWindow is null)
            return;

        if (!App.MainWindow.IsVisible)
        {
            App.MainWindow.Show();
            App.MainWindow.WindowState = WindowState.Normal;
            App.MainWindow.Activate();
        }

        _notificationManager ??= new WindowNotificationManager(App.MainWindow)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3
        };

        _notificationManager.Show(new Notification(title, message, type));
    }
}
