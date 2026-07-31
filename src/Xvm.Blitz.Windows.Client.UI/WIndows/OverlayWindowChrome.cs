using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

internal static class OverlayWindowChrome
{
    private const int GwlExStyle = -20;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExToolWindow = 0x00000080;

    public static void ExcludeFromAltTab(Window window)
    {
        window.ShowInTaskbar = false;
        window.Opened += OnOpened;
    }

    private static void OnOpened(object? sender, EventArgs _)
    {
        if (sender is Window window)
            ApplyToolWindowStyle(window);
    }

    private static void ApplyToolWindowStyle(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
        if (handle == nint.Zero)
            return;

        var exStyle = GetWindowLongPtr(handle, GwlExStyle);
        exStyle = (exStyle | WsExToolWindow) & ~WsExAppWindow;
        _ = SetWindowLongPtr(handle, GwlExStyle, exStyle);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
