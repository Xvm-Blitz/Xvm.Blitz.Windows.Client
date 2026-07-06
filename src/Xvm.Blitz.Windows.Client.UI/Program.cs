using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.ReactiveUI;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureConsoleEncoding();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureConsoleEncoding()
    {
        if (OperatingSystem.IsWindows())
        {
            TrySetWindowsConsoleCodePage(65001);
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
        }
    }

    private static void TrySetWindowsConsoleCodePage(uint codePage)
    {
        try
        {
            SetConsoleOutputCP(codePage);
            SetConsoleCP(codePage);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint codePage);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint codePage);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();
    }
}