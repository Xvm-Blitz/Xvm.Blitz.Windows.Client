using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class MainWindow : Window
{
    public required AppSettings AppSettings { get; init; }

    public required MainViewModel ViewModel { get; init; }

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += OnWindowSizeChanged;
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        var openReplaysButton = this.FindControl<Button>("OpenReplaysButton");
        if (openReplaysButton is null)
            return;

        const double headerMinWidth = 450;

        openReplaysButton.HorizontalAlignment = eventArgs.NewSize.Width < headerMinWidth
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Right;
    }
}
