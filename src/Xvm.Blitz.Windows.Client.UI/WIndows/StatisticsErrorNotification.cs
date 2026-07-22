namespace Xvm.Blitz.Windows.Client.UI.Windows;

public static class StatisticsErrorNotification
{
    public static Task Notify(string message) =>
        AppNotification.ShowError("Ошибка запроса статистики", message);
}
