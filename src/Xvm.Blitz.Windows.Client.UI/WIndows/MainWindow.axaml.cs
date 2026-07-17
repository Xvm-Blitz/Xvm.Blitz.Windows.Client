using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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

    private void HotkeyTextBox_KeyDown(object? _, KeyEventArgs eventArgs)
    {
        eventArgs.Handled = true;

        var modifiers = new List<string>();
        if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
            modifiers.Add("Ctrl");

        if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt))
            modifiers.Add("Alt");

        if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers.Add("Shift");

        var mainKey = eventArgs.Key switch
        {
            >= Key.A and <= Key.Z => eventArgs.Key.ToString(),
            // ReSharper disable once PatternIsRedundant
            >= Key.D0 and <= Key.D9 => eventArgs.Key.ToString()[1..],
            >= Key.F1 and <= Key.F12 => eventArgs.Key.ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => eventArgs.Key.ToString()[6..],
            Key.Space => "Space",
            _ => ""
        };

        if (string.IsNullOrEmpty(mainKey) || DataContext is not MainViewModel viewModel)
            return;

        viewModel.HideStatisticsCtrl = modifiers.Contains("Ctrl");
        viewModel.HideStatisticsAlt = modifiers.Contains("Alt");
        viewModel.HideStatisticsShift = modifiers.Contains("Shift");
        viewModel.HideStatisticsHotkey = mainKey;

        App.UpdateGlobalHotkey();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        var coordinatesGrid = this.FindControl<UniformGrid>("CoordinatesGrid");
        var alliesBorder = this.FindControl<Border>("AlliesBorder");
        var enemiesBorder = this.FindControl<Border>("EnemiesBorder");
        var openReplaysButton = this.FindControl<Button>("OpenReplaysButton");
        var hotkeyGrid = this.FindControl<Grid>("HotkeyGrid");
        var hotkeyLabel = this.FindControl<TextBlock>("HotkeyLabel");
        var hotkeyInputPanel = this.FindControl<StackPanel>("HotkeyInputPanel");

        if (coordinatesGrid is null || alliesBorder is null || enemiesBorder is null)
            return;

        const double minBlockWidth = 200;
        const double marginBetweenBlocks = 10;
        const double totalMinWidth = minBlockWidth * 2 + marginBetweenBlocks;
        const double headerMinWidth = 450;

        if (eventArgs.NewSize.Width < totalMinWidth)
        {
            coordinatesGrid.Columns = 1;
            coordinatesGrid.Rows = 2;

            alliesBorder.Margin = new Thickness(0, 0, 0, 5);
            enemiesBorder.Margin = new Thickness(0, 0, 0, 5);
        }
        else
        {
            coordinatesGrid.Columns = 2;
            coordinatesGrid.Rows = 1;

            alliesBorder.Margin = new Thickness(0, 0, 2.5, 5);
            enemiesBorder.Margin = new Thickness(2.5, 0, 0, 5);
        }

        if (openReplaysButton is not null)
        {
            openReplaysButton.HorizontalAlignment = eventArgs.NewSize.Width < headerMinWidth
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Right;
        }

        if (hotkeyGrid is null || hotkeyLabel is null || hotkeyInputPanel is null)
            return;

        if (eventArgs.NewSize.Width < headerMinWidth)
        {
            hotkeyGrid.RowDefinitions.Clear();
            hotkeyGrid.ColumnDefinitions.Clear();
            hotkeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hotkeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(hotkeyLabel, 0);
            Grid.SetColumn(hotkeyLabel, 0);
            hotkeyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hotkeyLabel.Margin = new Thickness(0, 5, 0, 5);

            Grid.SetRow(hotkeyInputPanel, 1);
            Grid.SetColumn(hotkeyInputPanel, 0);
            hotkeyInputPanel.HorizontalAlignment = HorizontalAlignment.Center;
            hotkeyInputPanel.Margin = new Thickness(0);
        }
        else
        {
            hotkeyGrid.RowDefinitions.Clear();
            hotkeyGrid.ColumnDefinitions.Clear();
            hotkeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hotkeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(hotkeyLabel, 0);
            Grid.SetColumn(hotkeyLabel, 0);
            hotkeyLabel.HorizontalAlignment = HorizontalAlignment.Left;
            hotkeyLabel.Margin = new Thickness(0, 5, 10, 0);

            Grid.SetRow(hotkeyInputPanel, 0);
            Grid.SetColumn(hotkeyInputPanel, 1);
            hotkeyInputPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            hotkeyInputPanel.Margin = new Thickness(0);
        }
    }

    private void AlliesPosition_ValueChanged(object? _, NumericUpDownValueChangedEventArgs eventArgs)
    {
        if (App.AlliesWindow is null || !eventArgs.NewValue.HasValue)
            return;

        var alliesXNumeric = this.FindControl<NumericUpDown>("AlliesXNumeric");
        var alliesYNumeric = this.FindControl<NumericUpDown>("AlliesYNumeric");

        var x = (int)(alliesXNumeric?.Value ?? 0);
        var y = (int)(alliesYNumeric?.Value ?? 0);

        AppSettings.AlliesWindowX = x;
        AppSettings.AlliesWindowY = y;

        App.AlliesWindow.Position = new PixelPoint(x, y);
    }

    private void EnemiesPosition_ValueChanged(object? _, NumericUpDownValueChangedEventArgs eventArgs)
    {
        if (App.EnemiesWindow is null || !eventArgs.NewValue.HasValue)
            return;

        var enemiesXNumeric = this.FindControl<NumericUpDown>("EnemiesXNumeric");
        var enemiesYNumeric = this.FindControl<NumericUpDown>("EnemiesYNumeric");

        var x = (int)(enemiesXNumeric?.Value ?? 0);
        var y = (int)(enemiesYNumeric?.Value ?? 0);

        AppSettings.EnemiesWindowX = x;
        AppSettings.EnemiesWindowY = y;

        var leftPosition = new PixelPoint(x - (int)App.EnemiesWindow.Bounds.Width, y);
        App.EnemiesWindow.Position = leftPosition;
    }
}
