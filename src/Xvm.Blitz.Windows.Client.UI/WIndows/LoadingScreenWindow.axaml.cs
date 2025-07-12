using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class LoadingScreenWindow : Window
{
    public LoadingScreenWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
