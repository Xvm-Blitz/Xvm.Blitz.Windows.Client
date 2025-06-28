using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class BooleanToColorConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}