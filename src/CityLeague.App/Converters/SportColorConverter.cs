using System.Globalization;
using CityLeague.App.Helpers;

namespace CityLeague.App.Converters;

public class SportColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString();
        var light = string.Equals(parameter?.ToString(), "light", StringComparison.OrdinalIgnoreCase);
        return light ? SportColors.GetLightColor(key) : SportColors.GetColor(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
