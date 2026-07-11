using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class HomeViewModel(ICityLeagueApi api) : BaseViewModel
{
    private List<EventSummaryDto> _allEvents = [];

    public ObservableCollection<SportDto> Sports { get; } = [];
    public ObservableCollection<EventSummaryDto> Events { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComingSoon))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private SportDto? selectedSport;

    [ObservableProperty]
    private bool isRefreshing;

    public bool IsComingSoon => SelectedSport is not null &&
        !string.Equals(SelectedSport.Availability, "Enabled", StringComparison.OrdinalIgnoreCase);

    public bool ShowEmptyState => !IsBusy && !IsComingSoon && Events.Count == 0;

    [RelayCommand]
    private async Task AppearingAsync()
    {
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

    partial void OnSelectedSportChanged(SportDto? value) => ApplyFilter();

    private void ApplyFilter()
    {
        Events.Clear();
        if (SelectedSport is not null && !IsComingSoon)
        {
            foreach (var e in _allEvents.Where(e => string.Equals(e.SportKey, SelectedSport.Key, StringComparison.OrdinalIgnoreCase)))
                Events.Add(e);
        }
        OnPropertyChanged(nameof(ShowEmptyState));
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
    private async Task OpenEventAsync(EventSummaryDto summary)
    {
        if (summary is null) return;
        await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={summary.Id}");
    }

    [RelayCommand]
    private async Task CreateAsync() => await Shell.Current.GoToAsync(AppRoutes.Create);
}
