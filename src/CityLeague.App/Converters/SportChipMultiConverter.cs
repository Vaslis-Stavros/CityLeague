using System.Globalization;
using CityLeague.App.Helpers;
using CityLeague.Core.Dtos;

namespace CityLeague.App.Converters;

/// <summary>Styles sport chips: neutral when unselected, sport color when selected.</summary>
public class SportChipMultiConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        var sport = values?.Length > 0 ? values[0] as SportDto : null;
        var selected = values?.Length > 1 ? values[1] as SportDto : null;
        var isSelected = sport is not null && selected is not null
            && string.Equals(sport.Key, selected.Key, StringComparison.OrdinalIgnoreCase);

        var part = parameter?.ToString() ?? "background";

        if (!isSelected)
        {
            return part switch
            {
                "background" => Colors.White,
                "text" => Color.FromArgb("#555555"),
                "stroke" => Color.FromArgb("#D8D8D8"),
                _ => Colors.Transparent,
            };
        }

        var sportColor = SportColors.GetColor(sport!.Key);
        return part switch
        {
            "background" => sportColor,
            "text" => Colors.White,
            "stroke" => sportColor,
            _ => sportColor,
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
