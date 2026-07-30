using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class HomeViewModel(ICityLeagueApi api, IAuthService auth) : BaseViewModel
{
    private List<EventSummaryDto> _upcoming = [];
    private List<EventSummaryDto> _incomplete = [];
    private List<EventSummaryDto> _pending = [];

    public ObservableCollection<SportDto> Sports { get; } = [];
    public ObservableCollection<HomeMatchItem> PendingResults { get; } = [];
    public ObservableCollection<HomeMatchItem> Matches { get; } = [];
    public ObservableCollection<HomeMatchItem> IncompleteMatches { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComingSoon))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ScheduleSubtitle))]
    [NotifyPropertyChangedFor(nameof(CanCreateMatch))]
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
            if (HasPendingResults)
                return "Set the pending result before creating another match.";
            if (!HasUpcoming && !HasIncomplete)
                return "No matches on the board.";
            var parts = new List<string>();
            if (HasUpcoming)
                parts.Add(Matches.Count == 1 ? "1 upcoming" : $"{Matches.Count} upcoming");
            if (HasIncomplete)
                parts.Add(IncompleteMatches.Count == 1 ? "1 incomplete" : $"{IncompleteMatches.Count} incomplete");
            return string.Join(" · ", parts);
        }
    }

    public bool IsComingSoon => SelectedSport is not null &&
        !string.Equals(SelectedSport.Availability, "Enabled", StringComparison.OrdinalIgnoreCase);

    public bool ShowEmptyState => !IsBusy && !IsComingSoon && !HasUpcoming && !HasIncomplete && !HasPendingResults;

    public bool CanCreateMatch => !IsComingSoon && !HasPendingResults;

    public bool HasPendingResults => PendingResults.Count > 0;
    public bool HasIncomplete => IncompleteMatches.Count > 0;
    public bool HasUpcoming => Matches.Count > 0;

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
            var upcomingTask = api.GetEventsAsync();
            var incompleteTask = api.GetIncompleteEventsAsync();
            var pendingTask = api.GetPendingResultEventsAsync();
            await Task.WhenAll(upcomingTask, incompleteTask, pendingTask);
            _upcoming = (await upcomingTask).ToList();
            _incomplete = (await incompleteTask).ToList();
            _pending = (await pendingTask).ToList();
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
        PendingResults.Clear();
        Matches.Clear();
        IncompleteMatches.Clear();

        if (SelectedSport is not null && !IsComingSoon)
        {
            foreach (var e in _pending
                         .Where(e => string.Equals(e.SportKey, SelectedSport.Key, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(e => e.ScheduledAt))
                PendingResults.Add(new HomeMatchItem(e, HomeMatchKind.PendingResult));

            foreach (var e in _upcoming
                         .Where(e => string.Equals(e.SportKey, SelectedSport.Key, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(e => e.ScheduledAt))
                Matches.Add(new HomeMatchItem(e));

            foreach (var e in _incomplete
                         .Where(e => string.Equals(e.SportKey, SelectedSport.Key, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(e => e.ScheduledAt))
                IncompleteMatches.Add(new HomeMatchItem(e, HomeMatchKind.Incomplete));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ScheduleSubtitle));
        OnPropertyChanged(nameof(CanCreateMatch));
        OnPropertyChanged(nameof(HasPendingResults));
        OnPropertyChanged(nameof(HasUpcoming));
        OnPropertyChanged(nameof(HasIncomplete));
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
        if (!CanCreateMatch)
        {
            await Shell.Current.DisplayAlert(
                "Result needed",
                "Submit the pending match result before creating another event.",
                "OK");
            return;
        }

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
    private async Task CreateAsync() => await CreateMatchAsync();
}
