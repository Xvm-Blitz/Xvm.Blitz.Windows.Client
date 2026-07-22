using System.Runtime.InteropServices;

namespace Xvm.Blitz.Windows.Client.Core.WindowsApis;

public static class WindowsApi
{
    private const int SW_SHOW = 5;

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowRect(IntPtr hWnd, ref WindowRectangle windowRectangle);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr hWnd, bool fUnknown);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    public static async Task<bool> ForceSetForegroundWindow(IntPtr windowHandle)
    {
        try
        {
            if (!IsWindow(windowHandle))
                return false;

            var currentForeground = GetForegroundWindow();
            if (currentForeground == windowHandle)
                return true;

            if (IsIconic(windowHandle))
            {
                ShowWindow(windowHandle, SW_RESTORE);

                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            if (SetForegroundWindow(windowHandle))
                return true;

            try
            {
                var currentThreadId = GetCurrentThreadId();
                var foregroundThreadId = GetWindowThreadProcessId(currentForeground, out _);

                var needDetach = false;
                if (foregroundThreadId != currentThreadId)
                    needDetach = AttachThreadInput(currentThreadId, foregroundThreadId, true);

                BringWindowToTop(windowHandle);
                ShowWindow(windowHandle, SW_SHOW);
                var result = SetForegroundWindow(windowHandle);

                if (needDetach)
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);

                if (result)
                    return true;
            }
            catch
            {
                // ignore
            }

            try
            {
                SwitchToThisWindow(windowHandle, true);

                return GetForegroundWindow() == windowHandle;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return SetForegroundWindow(windowHandle);
        }
    }
}
