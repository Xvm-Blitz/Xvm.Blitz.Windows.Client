using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class AlliesWindow : Window
{
    public AlliesWindow()
    {
        AvaloniaXamlLoader.Load(this);
        OverlayWindowChrome.ExcludeFromAltTab(this);
    }

    private void Window_PointerPressed(object? _, PointerPressedEventArgs eventArgs) =>
        OverlayWindowInteractions.BeginMove(this, eventArgs, "Allies");

    private void ResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control handle)
            OverlayWindowInteractions.BeginResize(handle, eventArgs);
    }

    private void HidePanels_Click(object? _, RoutedEventArgs __) =>
        App.MainWindow?.ViewModel.HidePanels();

    private void PlayerRow_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: ViewModels.Models.PlayerViewModel player } &&
            DataContext is BattleStatisticsViewModel viewModel &&
            viewModel.TrySelectCallPlayer(player))
        {
            eventArgs.Handled = true;
        }
    }

    private void CallSelected_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is BattleStatisticsViewModel viewModel)
            viewModel.InviteSelectedPlayer();
    }
}
