using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class EnemiesWindow : Window
{
    public EnemiesWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Window_PointerPressed(object? _, PointerPressedEventArgs eventArgs) =>
        OverlayWindowInteractions.BeginMove(this, eventArgs, "Enemies");

    private void ResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is Control handle)
            OverlayWindowInteractions.BeginResize(handle, eventArgs);
    }
}
