using Avalonia.Threading;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public static class LoadingScreenNotification
{
    public static Task NotifyLoadingScreenRequired()
    {
        var completionSource = new TaskCompletionSource();
        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    if (App.MainWindow?.DataContext is MainViewModel mainViewModel)
                        mainViewModel.NotifyLoadingScreenRequired();

                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

        return Task.WhenAll(
            completionSource.Task,
            AppNotification.ShowWarning(
                "Требуется замена экрана загрузки",
                "Распознавание статистики недоступно, пока экран загрузки боя не заменён."));
    }
}
