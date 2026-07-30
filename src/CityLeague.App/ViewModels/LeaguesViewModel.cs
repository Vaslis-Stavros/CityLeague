using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class LeaguesViewModel(ICityLeagueApi api) : BaseViewModel
{
    public ObservableCollection<LeagueDto> Leagues { get; } = [];
    public ObservableCollection<SportDto> Sports { get; } = [];

    [ObservableProperty]
    private bool showCreatePanel;

    [ObservableProperty]
    private string newLeagueName = string.Empty;

    [ObservableProperty]
    private string team1Name = string.Empty;

    [ObservableProperty]
    private string team2Name = string.Empty;

    [ObservableProperty]
    private int plannedMatchCount = 10;

    [ObservableProperty]
    private SportDto? selectedSport;

    [ObservableProperty]
    private bool isRefreshing;

    public bool ShowEmptyLeagues => Leagues.Count == 0;

    public string LeaguesSubtitle => Leagues.Count switch
    {
        0 => "Start a league for your crew",
        1 => "1 league",
        _ => $"{Leagues.Count} leagues",
    };

    public string PlannedMatchLabel => $"{PlannedMatchCount} match{(PlannedMatchCount == 1 ? "" : "es")}";

    partial void OnPlannedMatchCountChanged(int value) => OnPropertyChanged(nameof(PlannedMatchLabel));

    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (Sports.Count == 0)
        {
            await RunAsync(async () =>
            {
                var sports = await api.GetSportsAsync();
                Sports.Clear();
                foreach (var s in sports)
                    Sports.Add(s);
                SelectedSport = Sports.FirstOrDefault(s =>
                    string.Equals(s.Key, "football", StringComparison.OrdinalIgnoreCase)) ?? Sports.FirstOrDefault();
            });
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var leagues = await api.GetLeaguesAsync();
            Leagues.Clear();
            foreach (var l in leagues)
                Leagues.Add(l);
            OnPropertyChanged(nameof(ShowEmptyLeagues));
            OnPropertyChanged(nameof(LeaguesSubtitle));
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
    private void ToggleCreate() => ShowCreatePanel = !ShowCreatePanel;

    [RelayCommand]
    private void IncrementMatches()
    {
        if (PlannedMatchCount < 200)
            PlannedMatchCount++;
    }

    [RelayCommand]
    private void DecrementMatches()
    {
        if (PlannedMatchCount > 1)
            PlannedMatchCount--;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewLeagueName))
        {
            ErrorMessage = "Enter a league name.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Team1Name) || string.IsNullOrWhiteSpace(Team2Name))
        {
            ErrorMessage = "Name both teams.";
            return;
        }
        if (SelectedSport is null)
        {
            ErrorMessage = "Choose a sport.";
            return;
        }

        await RunAsync(async () =>
        {
            var created = await api.CreateLeagueAsync(new CreateLeagueRequest(
                NewLeagueName.Trim(),
                SelectedSport.Id,
                Team1Name.Trim(),
                Team2Name.Trim(),
                PlannedMatchCount));
            Leagues.Insert(0, created);
            NewLeagueName = string.Empty;
            Team1Name = string.Empty;
            Team2Name = string.Empty;
            PlannedMatchCount = 10;
            ShowCreatePanel = false;
            OnPropertyChanged(nameof(ShowEmptyLeagues));
            OnPropertyChanged(nameof(LeaguesSubtitle));
            await Shell.Current.GoToAsync($"{AppRoutes.LeagueDetail}?leagueId={created.Id}");
        });
    }

    [RelayCommand]
    private async Task OpenLeagueAsync(LeagueDto league)
    {
        if (league is null) return;
        await Shell.Current.GoToAsync($"{AppRoutes.LeagueDetail}?leagueId={league.Id}");
    }

    [RelayCommand]
    private async Task DeleteLeagueAsync(LeagueDto league)
    {
        if (league is null || !league.CanDelete) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Delete league?",
            "This league has no finished matches and will be permanently removed.",
            "Delete", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () =>
        {
            await api.DeleteLeagueAsync(league.Id);
            Leagues.Remove(league);
            OnPropertyChanged(nameof(ShowEmptyLeagues));
            OnPropertyChanged(nameof(LeaguesSubtitle));
        });
    }

    [RelayCommand]
    private async Task EndLeagueAsync(LeagueDto league)
    {
        if (league is null || !league.CanEnd) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "End league?",
            "Finish this league now and move it to History. Team leaders can do this early.",
            "End league", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () =>
        {
            await api.EndLeagueAsync(league.Id);
            Leagues.Remove(league);
            OnPropertyChanged(nameof(ShowEmptyLeagues));
            OnPropertyChanged(nameof(LeaguesSubtitle));
        });
    }
}
