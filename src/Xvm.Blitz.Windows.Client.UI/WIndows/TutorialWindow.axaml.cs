using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class TutorialWindow : Window
{
    public TutorialWindow(TutorialViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public TutorialWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
