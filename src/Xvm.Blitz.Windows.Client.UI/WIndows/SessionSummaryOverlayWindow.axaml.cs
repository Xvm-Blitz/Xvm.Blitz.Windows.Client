using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class SessionSummaryOverlayWindow : Window
{
    public SessionSummaryOverlayWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Overlay_PointerPressed(object? _, PointerPressedEventArgs eventArgs) =>
        OverlayWindowInteractions.BeginMove(this, eventArgs, "SessionSummary");

    private void ResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control handle)
            OverlayWindowInteractions.BeginSessionSummaryOverlayResize(handle, eventArgs);
    }

    private void HideOverlay_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.HideSessionSummaryOverlay();
    }
}
