using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class AuthorizationWindow : Window
{
    public AuthorizationWindow(AuthorizationViewModel model) : this()
    {
        DataContext = model;
    }

    public AuthorizationWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
