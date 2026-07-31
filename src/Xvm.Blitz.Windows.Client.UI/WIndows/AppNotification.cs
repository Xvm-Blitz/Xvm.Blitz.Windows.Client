using System.Runtime.InteropServices;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public static class AppNotification
{
    public const string AppUserModelId = "Xvm.Blitz.Windows.Client";

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
                    ShowDesktopToast(title, message);
                    ShowInAppFallback(title, message, type);
                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

        return completionSource.Task;
    }

    private static void ShowDesktopToast(string title, string message) =>
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show(toast => toast.ExpirationTime = DateTimeOffset.Now.AddSeconds(15));

    private static void ShowInAppFallback(string title, string message, NotificationType type)
    {
        if (App.MainWindow is null || !App.MainWindow.IsVisible)
            return;

        _notificationManager ??= new WindowNotificationManager(App.MainWindow)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3
        };

        _notificationManager.Show(new Notification(title, message, type));
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    public static void RegisterAppUserModelId() =>
        _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
}
