namespace CityLeague.App.Services;

public enum AppColorTheme
{
    Dark = 0,
    Light = 1,
}

public enum DateTimeDisplayFormat
{
    Relative = 0,
    Short = 1,
    Medium = 2,
    Long = 3,
}

public interface IAppPreferences
{
    AppColorTheme ColorTheme { get; set; }
    DateTimeDisplayFormat DateTimeFormat { get; set; }
    bool Use24HourClock { get; set; }
    bool ReduceMotion { get; set; }
    bool ShowWeekdayInDates { get; set; }

    event EventHandler? Changed;

    void ApplyToApp();
    string FormatDateTime(DateTimeOffset value);
    bool IsLight { get; }
}

/// <summary>Persists appearance preferences with <see cref="Preferences"/>.</summary>
public sealed class AppPreferences : IAppPreferences
{
    private const string ThemeKey = "prefs.color_theme";
    private const string DateFormatKey = "prefs.datetime_format";
    private const string Clock24Key = "prefs.clock_24h";
    private const string ReduceMotionKey = "prefs.reduce_motion";
    private const string WeekdayKey = "prefs.show_weekday";

    public event EventHandler? Changed;

    public AppColorTheme ColorTheme
    {
        get => (AppColorTheme)Preferences.Default.Get(ThemeKey, (int)AppColorTheme.Dark);
        set
        {
            if (ColorTheme == value) return;
            Preferences.Default.Set(ThemeKey, (int)value);
            Raise();
        }
    }

    public DateTimeDisplayFormat DateTimeFormat
    {
        get => (DateTimeDisplayFormat)Preferences.Default.Get(DateFormatKey, (int)DateTimeDisplayFormat.Medium);
        set
        {
            if (DateTimeFormat == value) return;
            Preferences.Default.Set(DateFormatKey, (int)value);
            Raise();
        }
    }

    public bool Use24HourClock
    {
        get => Preferences.Default.Get(Clock24Key, true);
        set
        {
            if (Use24HourClock == value) return;
            Preferences.Default.Set(Clock24Key, value);
            Raise();
        }
    }

    public bool ReduceMotion
    {
        get => Preferences.Default.Get(ReduceMotionKey, false);
        set
        {
            if (ReduceMotion == value) return;
            Preferences.Default.Set(ReduceMotionKey, value);
            Raise();
        }
    }

    public bool ShowWeekdayInDates
    {
        get => Preferences.Default.Get(WeekdayKey, true);
        set
        {
            if (ShowWeekdayInDates == value) return;
            Preferences.Default.Set(WeekdayKey, value);
            Raise();
        }
    }

    public bool IsLight => ColorTheme == AppColorTheme.Light;

    public void ApplyToApp()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is null) return;
            Application.Current.UserAppTheme = IsLight ? AppTheme.Light : AppTheme.Dark;

            if (Shell.Current is not null)
            {
                if (IsLight)
                {
                    Shell.Current.SetValue(Shell.TabBarBackgroundColorProperty, Color.FromArgb("#F2F4F7"));
                    Shell.Current.SetValue(Shell.TabBarForegroundColorProperty, Color.FromArgb("#0B6B2E"));
                    Shell.Current.SetValue(Shell.TabBarTitleColorProperty, Color.FromArgb("#0B6B2E"));
                    Shell.Current.SetValue(Shell.TabBarUnselectedColorProperty, Color.FromArgb("#6B7785"));
                    Shell.Current.SetValue(Shell.BackgroundColorProperty, Color.FromArgb("#F2F4F7"));
                }
                else
                {
                    Shell.Current.SetValue(Shell.TabBarBackgroundColorProperty, Color.FromArgb("#152033"));
                    Shell.Current.SetValue(Shell.TabBarForegroundColorProperty, Color.FromArgb("#3DDC84"));
                    Shell.Current.SetValue(Shell.TabBarTitleColorProperty, Color.FromArgb("#3DDC84"));
                    Shell.Current.SetValue(Shell.TabBarUnselectedColorProperty, Color.FromArgb("#8FA0B5"));
                    Shell.Current.SetValue(Shell.BackgroundColorProperty, Color.FromArgb("#0E1525"));
                }
            }

            Helpers.StatusBarTheme.RefreshCurrentPage();
        });
    }

    public string FormatDateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var time = Use24HourClock ? local.ToString("HH:mm") : local.ToString("h:mm tt");
        var weekday = ShowWeekdayInDates ? local.ToString("ddd") + " " : string.Empty;

        return DateTimeFormat switch
        {
            DateTimeDisplayFormat.Relative => FormatRelative(local) + " · " + time,
            DateTimeDisplayFormat.Short => $"{weekday}{local:d} · {time}",
            DateTimeDisplayFormat.Long => $"{weekday}{local:D} · {time}",
            _ => $"{weekday}{local:MMM d, yyyy} · {time}",
        };
    }

    private static string FormatRelative(DateTimeOffset local)
    {
        var today = DateTimeOffset.Now.Date;
        var day = local.Date;
        var delta = (day - today).Days;
        return delta switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            >= 2 and < 7 => local.ToString("dddd"),
            _ => local.ToString("MMM d"),
        };
    }

    private void Raise()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        ApplyToApp();
    }
}
