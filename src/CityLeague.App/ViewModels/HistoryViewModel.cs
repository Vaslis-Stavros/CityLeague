using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class HistoryViewModel(ICityLeagueApi api) : BaseViewModel
{
    public ObservableCollection<EventSummaryDto> PastEvents { get; } = [];
    public ObservableCollection<LeagueDto> CompletedLeagues { get; } = [];

    [ObservableProperty]
    private bool isRefreshing;

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var events = await api.GetPastEventsAsync();
            PastEvents.Clear();
            foreach (var e in events)
                PastEvents.Add(e);

            var leagues = await api.GetCompletedLeaguesAsync();
            CompletedLeagues.Clear();
            foreach (var l in leagues)
                CompletedLeagues.Add(l);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private async Task OpenPastEventAsync(EventSummaryDto summary)
    {
        if (summary is null) return;
        await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={summary.Id}&readOnly=true");
    }
}
