using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class SessionSummaryOverlayWindow : Window
{
    private const double ResizeCornerSize = 28;

    public SessionSummaryOverlayWindow()
    {
        AvaloniaXamlLoader.Load(this);
        OverlayWindowChrome.ExcludeFromAltTab(this);
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.Handled)
            return;

        if (DataContext is MainViewModel { IsDisplayConfigurationMode: true } &&
            IsCornerResize(eventArgs))
        {
            OverlayWindowInteractions.BeginSessionSummaryOverlayResize(this, eventArgs);
            return;
        }

        OverlayWindowInteractions.BeginMove(this, eventArgs, "SessionSummary");
    }

    private void ResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control handle)
            return;

        OverlayWindowInteractions.BeginSessionSummaryOverlayResize(handle, eventArgs);
    }

    private void HideOverlay_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.HideSessionSummaryOverlay();
    }

    private bool IsCornerResize(PointerPressedEventArgs eventArgs)
    {
        var point = eventArgs.GetPosition(this);
        return point.X >= Bounds.Width - ResizeCornerSize &&
               point.Y >= Bounds.Height - ResizeCornerSize;
    }
}
