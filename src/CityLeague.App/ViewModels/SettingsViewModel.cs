using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Helpers;
using CityLeague.App.Services;

namespace CityLeague.App.ViewModels;

public partial class SettingsViewModel(IAppPreferences prefs) : BaseViewModel
{
    private bool _loading;

    public ObservableCollection<string> DateFormatOptions { get; } =
    [
        "Relative (Today · 18:30)",
        "Short (3/15/26 · 18:30)",
        "Medium (Mar 15, 2026 · 18:30)",
        "Long (Sunday, March 15 · 18:30)",
    ];

    [ObservableProperty]
    private bool isDarkTheme = true;

    [ObservableProperty]
    private int selectedDateFormatIndex;

    [ObservableProperty]
    private bool use24HourClock = true;

    [ObservableProperty]
    private bool reduceMotion;

    [ObservableProperty]
    private bool showWeekdayInDates = true;

    [ObservableProperty]
    private string previewDateText = string.Empty;

    public string ThemeSubtitle => IsDarkTheme ? "Dark glass" : "Light glass";

    [RelayCommand]
    private void Appearing()
    {
        _loading = true;
        IsDarkTheme = prefs.ColorTheme == AppColorTheme.Dark;
        SelectedDateFormatIndex = (int)prefs.DateTimeFormat;
        Use24HourClock = prefs.Use24HourClock;
        ReduceMotion = prefs.ReduceMotion;
        ShowWeekdayInDates = prefs.ShowWeekdayInDates;
        _loading = false;
        RefreshPreview();
        OnPropertyChanged(nameof(ThemeSubtitle));
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_loading) return;
        prefs.ColorTheme = value ? AppColorTheme.Dark : AppColorTheme.Light;
        OnPropertyChanged(nameof(ThemeSubtitle));
    }

    partial void OnSelectedDateFormatIndexChanged(int value)
    {
        if (_loading || value is < 0 or > 3) return;
        prefs.DateTimeFormat = (DateTimeDisplayFormat)value;
        RefreshPreview();
    }

    partial void OnUse24HourClockChanged(bool value)
    {
        if (_loading) return;
        prefs.Use24HourClock = value;
        RefreshPreview();
    }

    partial void OnReduceMotionChanged(bool value)
    {
        if (_loading) return;
        prefs.ReduceMotion = value;
    }

    partial void OnShowWeekdayInDatesChanged(bool value)
    {
        if (_loading) return;
        prefs.ShowWeekdayInDates = value;
        RefreshPreview();
    }

    private void RefreshPreview()
        => PreviewDateText = prefs.FormatDateTime(DateTimeOffset.Now.AddDays(1).AddHours(2));
}
