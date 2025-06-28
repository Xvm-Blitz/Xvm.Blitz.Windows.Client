using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class AlliesWindow : Window
{
    private const string WindowName = "Allies";

    private Point _lastPosition;

    public AlliesWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Window_PointerPressed(object? _, PointerPressedEventArgs e)
    {
        if (App.MainWindow?.ViewModel.IsDisplayConfigurationMode != true)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _lastPosition = e.GetPosition(this);
        PointerMoved += Window_PointerMoved;
        PointerReleased += Window_PointerReleased;
    }

    private void Window_PointerMoved(object? _, PointerEventArgs e)
    {
        var currentPosition = e.GetPosition(this);

        var newPosition = new PixelPoint(
            Position.X + (int)(currentPosition.X - _lastPosition.X),
            Position.Y + (int)(currentPosition.Y - _lastPosition.Y));

        if (App.MainWindow?.ViewModel is not null)
            Dispatcher.UIThread.Post(() => App.MainWindow.ViewModel.UpdateWindowPosition(WindowName, newPosition));
    }

    private void Window_PointerReleased(object? _, PointerReleasedEventArgs e)
    {
        PointerMoved -= Window_PointerMoved;
        PointerReleased -= Window_PointerReleased;
    }
}