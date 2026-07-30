using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class HomeViewModel(ICityLeagueApi api, IAuthService auth) : BaseViewModel
{
    private List<EventSummaryDto> _allEvents = [];

    public ObservableCollection<SportDto> Sports { get; } = [];
    public ObservableCollection<HomeMatchItem> Matches { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComingSoon))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ScheduleSubtitle))]
    [NotifyPropertyChangedFor(nameof(BackdropTop))]
    [NotifyPropertyChangedFor(nameof(BackdropMid))]
    [NotifyPropertyChangedFor(nameof(BackdropBottom))]
    [NotifyPropertyChangedFor(nameof(GlowColor))]
    [NotifyPropertyChangedFor(nameof(SoftTextColor))]
    [NotifyPropertyChangedFor(nameof(SoftMutedColor))]
    [NotifyPropertyChangedFor(nameof(AccentColor))]
    private SportDto? selectedSport;

    [ObservableProperty]
    private bool isRefreshing;

    public Color BackdropTop => Theme.Top;
    public Color BackdropMid => Theme.Mid;
    public Color BackdropBottom => Theme.Bottom;
    public Color GlowColor => Theme.Glow;
    public Color SoftTextColor => Theme.SoftText;
    public Color SoftMutedColor => Theme.SoftMuted;
    public Color AccentColor => Theme.Accent;

    private Helpers.SportColors.BackdropTheme Theme
        => Helpers.SportColors.GetTheme(SelectedSport?.Key);

    public string Greeting
    {
        get
        {
            var name = auth.CurrentUser?.DisplayName?.Trim();
            var first = string.IsNullOrWhiteSpace(name) ? null : name.Split(' ', 2)[0];
            var hour = DateTime.Now.Hour;
            var hello = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            return string.IsNullOrWhiteSpace(first) ? hello : $"{hello}, {first}";
        }
    }

    public string ScheduleTitle => IsComingSoon ? "Coming soon" : "Upcoming";

    public string ScheduleSubtitle
    {
        get
        {
            if (IsComingSoon)
                return "This sport isn’t ready yet — football is live.";
            if (Matches.Count == 0)
                return "No matches on the board.";
            return Matches.Count == 1 ? "1 match scheduled" : $"{Matches.Count} matches scheduled";
        }
    }

    public bool IsComingSoon => SelectedSport is not null &&
        !string.Equals(SelectedSport.Availability, "Enabled", StringComparison.OrdinalIgnoreCase);

    public bool ShowEmptyState => !IsBusy && !IsComingSoon && Matches.Count == 0;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        OnPropertyChanged(nameof(Greeting));
        if (Sports.Count == 0)
            await LoadAsync();
        else
            await RefreshEventsAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var sports = await api.GetSportsAsync();
            Sports.Clear();
            foreach (var sport in sports)
                Sports.Add(sport);

            SelectedSport ??= Sports.FirstOrDefault(s =>
                string.Equals(s.Availability, "Enabled", StringComparison.OrdinalIgnoreCase)) ?? Sports.FirstOrDefault();

            await RefreshEventsAsync();
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await RefreshEventsAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task RefreshEventsAsync()
    {
        try
        {
            _allEvents = (await api.GetEventsAsync()).ToList();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        ApplyFilter();
    }

    partial void OnSelectedSportChanged(SportDto? value)
    {
        OnPropertyChanged(nameof(ScheduleTitle));
        ApplyFilter();
    }

    protected override void OnBusyStateChanged(bool isBusy)
        => OnPropertyChanged(nameof(ShowEmptyState));

    private void ApplyFilter()
    {
        Matches.Clear();
        if (SelectedSport is not null && !IsComingSoon)
        {
            foreach (var e in _allEvents
                         .Where(e => string.Equals(e.SportKey, SelectedSport.Key, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(e => e.ScheduledAt))
                Matches.Add(new HomeMatchItem(e));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ScheduleSubtitle));
    }

    [RelayCommand]
    private Task SelectSportAsync(SportDto sport)
    {
        SelectedSport = sport;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreateMatchAsync()
    {
        var key = SelectedSport?.Key ?? "football";
        await Shell.Current.GoToAsync($"{AppRoutes.Create}?sport={Uri.EscapeDataString(key)}");
    }

    [RelayCommand]
    private async Task OpenEventAsync(HomeMatchItem item)
    {
        if (item?.Summary is null) return;
        await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={item.Summary.Id}");
    }

    [RelayCommand]
    private async Task CreateAsync() => await Shell.Current.GoToAsync(AppRoutes.Create);
}
