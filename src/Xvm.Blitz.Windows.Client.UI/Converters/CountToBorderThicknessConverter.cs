using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class CountToBorderThicknessConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is > 1 ? new Thickness(1) : new Thickness(0);
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