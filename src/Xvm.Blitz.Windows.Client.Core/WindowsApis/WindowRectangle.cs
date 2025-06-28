using System.Runtime.InteropServices;

namespace Xvm.Blitz.Windows.Client.Core.WindowsApis;

[StructLayout(LayoutKind.Sequential)]
public struct WindowRectangle
{
    public int Left;

    public int Top;

    public int Right;

    public int Bottom;
}