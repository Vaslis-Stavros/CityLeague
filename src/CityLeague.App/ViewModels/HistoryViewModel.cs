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

    public bool ShowEmptyPastEvents => PastEvents.Count == 0;
    public bool ShowEmptyCompletedLeagues => CompletedLeagues.Count == 0;

    public string HistorySubtitle
    {
        get
        {
            var matches = PastEvents.Count;
            var leagues = CompletedLeagues.Count;
            if (matches == 0 && leagues == 0)
                return "Finished matches and leagues land here";
            return $"{matches} match{(matches == 1 ? "" : "es")} · {leagues} league{(leagues == 1 ? "" : "s")}";
        }
    }

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

            OnPropertyChanged(nameof(ShowEmptyPastEvents));
            OnPropertyChanged(nameof(ShowEmptyCompletedLeagues));
            OnPropertyChanged(nameof(HistorySubtitle));
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
