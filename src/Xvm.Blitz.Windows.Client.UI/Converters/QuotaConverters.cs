using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class QuotaProgressColorConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is double percentage)
            return percentage switch
            {
                >= 95 => new SolidColorBrush(Colors.Red),
                >= 80 => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Green)
            };

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