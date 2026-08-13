using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class VoiceOverlayWindow : Window
{
    public VoiceOverlayWindow()
    {
        AvaloniaXamlLoader.Load(this);
        OverlayWindowChrome.ExcludeFromAltTab(this);
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (App.MainWindow?.ViewModel is { } viewModel && !viewModel.IsVoiceOverlayPositionSaved)
            Position = ResolveTopRightPosition();
        else if (App.MainWindow?.ViewModel is { } saved)
            Position = saved.VoiceOverlayPosition;

        ClampToScreen();
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (IsInteractiveSource(eventArgs.Source))
            return;

        OverlayWindowInteractions.BeginMove(this, eventArgs, "Voice");
    }

    private static bool IsInteractiveSource(object? source)
    {
        for (var current = source as Control; current is not null; current = current.Parent as Control)
        {
            if (current is Button or CheckBox)
                return true;
        }

        return false;
    }

    private void Accept_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is VoiceOverlayViewModel viewModel)
            viewModel.Accept();
    }

    private void Reject_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is VoiceOverlayViewModel viewModel)
            viewModel.Reject();
    }

    private void Hangup_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is VoiceOverlayViewModel viewModel)
            viewModel.Hangup();
    }

    private void Mute_Click(object? _, RoutedEventArgs __)
    {
        if (DataContext is VoiceOverlayViewModel viewModel)
            viewModel.ToggleMute();
    }

    public void ClampToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var width = double.IsNaN(Bounds.Width) || Bounds.Width <= 0 ? 260 : Bounds.Width;
        var height = double.IsNaN(Bounds.Height) || Bounds.Height <= 0 ? 120 : Bounds.Height;
        var x = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.X + area.Width - (int)width));
        var y = Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - (int)height));
        Position = new PixelPoint(x, y);
    }

    private PixelPoint ResolveTopRightPosition()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return new PixelPoint(24, 24);

        var area = screen.WorkingArea;
        var width = double.IsNaN(Bounds.Width) || Bounds.Width <= 0 ? 260 : Bounds.Width;
        return new PixelPoint(area.X + area.Width - (int)width - 24, area.Y + 24);
    }
}
