using System.Globalization;
using Avalonia.Data.Converters;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class CountToSpacingConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is int count and > 1)
            return 2.0;

        return 0.0;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}