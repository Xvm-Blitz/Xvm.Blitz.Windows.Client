using System.Globalization;
using Avalonia.Data.Converters;

namespace Xvm.Blitz.Windows.Client.UI.Converters;

public class BattlesToKiloConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not int numberOfBattles)
            return "—";

        if (numberOfBattles < 1000)
            return numberOfBattles.ToString();

        var thousands = numberOfBattles / 1000;
        return $"{thousands}k";
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