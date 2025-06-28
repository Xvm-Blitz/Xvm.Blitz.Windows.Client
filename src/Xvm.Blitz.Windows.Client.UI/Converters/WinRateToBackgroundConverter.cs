using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class WinRateToBackgroundConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is double winRate)
            return winRate switch
            {
                >= 70 => new SolidColorBrush(Color.Parse("#b497cc")),
                >= 60 => new SolidColorBrush(Color.Parse("#85bbf2")),
                >= 50 => new SolidColorBrush(Color.Parse("#93cf93")),
                _ => new SolidColorBrush(Color.Parse("#d68585"))
            };

        return new SolidColorBrush(Color.Parse("#333333"));
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