using System.Globalization;
using CityLeague.App.Helpers;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.Converters;

/// <summary>Styles sport chips for the glass home header.</summary>
public class SportChipMultiConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        var sport = values?.Length > 0 ? values[0] as SportDto : null;
        var selected = values?.Length > 1 ? values[1] as SportDto : null;
        var isSelected = sport is not null && selected is not null
            && string.Equals(sport.Key, selected.Key, StringComparison.OrdinalIgnoreCase);

        var part = parameter?.ToString() ?? "background";
        var light = false;
        try { light = ServiceHelper.GetService<IAppPreferences>().IsLight; } catch { /* ignore */ }

        if (!isSelected)
        {
            // Prefer live DynamicResource values when available.
            if (Application.Current?.Resources is { } res)
            {
                if (part == "background" && res.TryGetValue("ThemeChipFill", out var fill) && fill is Color fillColor)
                    return fillColor;
                if (part == "stroke" && res.TryGetValue("ThemeChipStroke", out var stroke) && stroke is Color strokeColor)
                    return strokeColor;
                if (part == "text" && res.TryGetValue("PageFaint", out var text) && text is Color textColor)
                    return textColor;
            }

            return part switch
            {
                "background" => light ? Color.FromArgb("#22000000") : Color.FromArgb("#28FFFFFF"),
                "text" => light ? Color.FromArgb("#14261A") : Color.FromArgb("#EAF7EE"),
                "stroke" => light ? Color.FromArgb("#33000000") : Color.FromArgb("#55FFFFFF"),
                _ => Colors.Transparent,
            };
        }

        var sportColor = SportColors.GetColor(sport!.Key);
        return part switch
        {
            "background" => Colors.White,
            "text" => sportColor,
            "stroke" => Colors.White,
            _ => sportColor,
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
