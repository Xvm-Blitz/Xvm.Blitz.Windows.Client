using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public static class LoadingScreenNotification
{
    private static WindowNotificationManager? _notificationManager;

    public static Task NotifyLoadingScreenRequired()
    {
        var completionSource = new TaskCompletionSource();
        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    Show();
                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

        return completionSource.Task;
    }

    private static void Show()
    {
        if (App.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NotifyLoadingScreenRequired();
        }

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

        _notificationManager.Show(
            new Notification(
                "Требуется замена экрана загрузки",
                "Распознавание статистики недоступно, пока экран загрузки боя не заменён.",
                NotificationType.Warning));
    }
}
