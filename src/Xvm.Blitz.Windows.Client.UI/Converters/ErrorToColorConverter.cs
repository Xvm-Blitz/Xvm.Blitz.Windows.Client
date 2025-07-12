using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class ErrorToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isError)
            return isError ? Brushes.Red : Brushes.Green;

        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
