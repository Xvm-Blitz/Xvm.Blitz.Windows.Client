using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

internal enum OverlayResizeAxis
{
    Horizontal,
    Vertical,
    Both
}

internal static class OverlayWindowInteractions
{
    public static void BeginMove(Window window, PointerPressedEventArgs eventArgs, string windowName)
    {
        if (!eventArgs.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
            return;

        eventArgs.Handled = true;
        var grabOffset = eventArgs.GetPosition(window);
        window.PointerMoved += OnMoveMoved;
        window.PointerReleased += OnMoveReleased;
        eventArgs.Pointer.Capture(window);

        void OnMoveMoved(object? _, PointerEventArgs moveEventArgs)
        {
            var currentPosition = moveEventArgs.GetPosition(window);
            var newPosition = new PixelPoint(
                window.Position.X + (int)(currentPosition.X - grabOffset.X),
                window.Position.Y + (int)(currentPosition.Y - grabOffset.Y));

            window.Position = newPosition;

            if (windowName == "Voice" && window is VoiceOverlayWindow voiceOverlay)
                voiceOverlay.ClampToScreen();

            if (App.MainWindow?.ViewModel is null)
                return;

            if (windowName == "Enemies")
            {
                var rightTopCornerPosition = new PixelPoint(
                    newPosition.X + (int)window.Bounds.Width,
                    newPosition.Y);
                Dispatcher.UIThread.Post(() => App.MainWindow.ViewModel.UpdateWindowPosition(windowName, rightTopCornerPosition));
                return;
            }

            Dispatcher.UIThread.Post(() => App.MainWindow.ViewModel.UpdateWindowPosition(windowName, newPosition));
        }

        void OnMoveReleased(object? sender, PointerReleasedEventArgs releaseEventArgs)
        {
            window.PointerMoved -= OnMoveMoved;
            window.PointerReleased -= OnMoveReleased;
            releaseEventArgs.Pointer.Capture(null);
        }
    }

    public static void BeginResize(Control handle, PointerPressedEventArgs eventArgs)
    {
        if (handle.DataContext is not BattleStatisticsViewModel viewModel)
            return;

        if (!eventArgs.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            return;

        eventArgs.Handled = true;
        var initialScaleX = viewModel.PanelScaleX;
        var initialScaleY = viewModel.PanelScaleY;
        var startPosition = eventArgs.GetPosition(null);

        handle.PointerMoved += OnResizeMoved;
        handle.PointerReleased += OnResizeReleased;
        eventArgs.Pointer.Capture(handle);

        void OnResizeMoved(object? _, PointerEventArgs moveEventArgs)
        {
            if (handle.DataContext is not BattleStatisticsViewModel resizeViewModel)
                return;

            var current = moveEventArgs.GetPosition(null);
            var deltaX = current.X - startPosition.X;
            var deltaY = current.Y - startPosition.Y;

            resizeViewModel.SetPanelScale(
                OverlayPanelSizing.ScaleXFromWidthDelta(initialScaleX, initialScaleY, deltaX),
                OverlayPanelSizing.ScaleYFromHeightDelta(initialScaleY, deltaY));
        }

        void OnResizeReleased(object? sender, PointerReleasedEventArgs releaseEventArgs)
        {
            handle.PointerMoved -= OnResizeMoved;
            handle.PointerReleased -= OnResizeReleased;
            releaseEventArgs.Pointer.Capture(null);

            if (handle.DataContext is BattleStatisticsViewModel resizeViewModel)
                resizeViewModel.PersistPanelScaleAndSave();
        }
    }

    public static void BeginSessionSummaryOverlayResize(
        Control handle,
        PointerPressedEventArgs eventArgs,
        OverlayResizeAxis axis = OverlayResizeAxis.Both)
    {
        if (App.MainWindow?.ViewModel is not MainViewModel viewModel)
            return;

        if (!eventArgs.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            return;

        eventArgs.Handled = true;
        var initialScaleX = viewModel.SessionSummaryOverlayScaleX;
        var initialScaleY = viewModel.SessionSummaryOverlayScaleY;
        var startPosition = eventArgs.GetPosition(null);

        handle.PointerMoved += OnResizeMoved;
        handle.PointerReleased += OnResizeReleased;
        eventArgs.Pointer.Capture(handle);

        void OnResizeMoved(object? _, PointerEventArgs moveEventArgs)
        {
            if (App.MainWindow?.ViewModel is not MainViewModel resizeViewModel)
                return;

            var (deltaX, deltaY) = ResolveResizeDelta(moveEventArgs, startPosition, axis);
            resizeViewModel.SetSessionSummaryOverlayScale(
                OverlayPanelSizing.SessionOverlayScaleXFromWidthDelta(initialScaleX, initialScaleY, deltaX),
                OverlayPanelSizing.SessionOverlayScaleYFromHeightDelta(initialScaleY, deltaY));
        }

        void OnResizeReleased(object? sender, PointerReleasedEventArgs releaseEventArgs)
        {
            handle.PointerMoved -= OnResizeMoved;
            handle.PointerReleased -= OnResizeReleased;
            releaseEventArgs.Pointer.Capture(null);

            if (App.MainWindow?.ViewModel is MainViewModel resizeViewModel)
                resizeViewModel.PersistSessionSummaryOverlayScaleAndSave();
        }
    }

    public static void BeginVoiceOverlayResize(
        Control handle,
        PointerPressedEventArgs eventArgs,
        OverlayResizeAxis axis = OverlayResizeAxis.Both)
    {
        if (handle.DataContext is not VoiceOverlayViewModel viewModel)
            return;

        if (!eventArgs.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            return;

        eventArgs.Handled = true;
        var initialScaleX = viewModel.OverlayScaleX;
        var initialScaleY = viewModel.OverlayScaleY;
        var startPosition = eventArgs.GetPosition(null);

        handle.PointerMoved += OnResizeMoved;
        handle.PointerReleased += OnResizeReleased;
        eventArgs.Pointer.Capture(handle);

        void OnResizeMoved(object? _, PointerEventArgs moveEventArgs)
        {
            if (handle.DataContext is not VoiceOverlayViewModel resizeViewModel)
                return;

            var (deltaX, deltaY) = ResolveResizeDelta(moveEventArgs, startPosition, axis);
            resizeViewModel.SetScale(
                OverlayPanelSizing.VoiceOverlayScaleXFromWidthDelta(initialScaleX, initialScaleY, deltaX),
                OverlayPanelSizing.VoiceOverlayScaleYFromHeightDelta(initialScaleY, deltaY));
        }

        void OnResizeReleased(object? sender, PointerReleasedEventArgs releaseEventArgs)
        {
            handle.PointerMoved -= OnResizeMoved;
            handle.PointerReleased -= OnResizeReleased;
            releaseEventArgs.Pointer.Capture(null);

            if (handle.DataContext is VoiceOverlayViewModel resizeViewModel)
                resizeViewModel.PersistScaleAndSave();
        }
    }

    public static OverlayResizeAxis ParseResizeAxis(object? tag) =>
        (tag as string) switch
        {
            "Horizontal" => OverlayResizeAxis.Horizontal,
            "Vertical" => OverlayResizeAxis.Vertical,
            _ => OverlayResizeAxis.Both
        };

    private static (double DeltaX, double DeltaY) ResolveResizeDelta(
        PointerEventArgs moveEventArgs,
        Point startPosition,
        OverlayResizeAxis axis)
    {
        var current = moveEventArgs.GetPosition(null);
        var deltaX = axis is OverlayResizeAxis.Vertical ? 0 : current.X - startPosition.X;
        var deltaY = axis is OverlayResizeAxis.Horizontal ? 0 : current.Y - startPosition.Y;
        return (deltaX, deltaY);
    }
}
