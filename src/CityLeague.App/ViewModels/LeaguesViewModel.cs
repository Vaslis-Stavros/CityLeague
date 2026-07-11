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
    private SportDto? selectedSport;

    [ObservableProperty]
    private bool isRefreshing;

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
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewLeagueName))
        {
            ErrorMessage = "Enter a league name.";
            return;
        }
        if (SelectedSport is null)
        {
            ErrorMessage = "Choose a sport.";
            return;
        }

        await RunAsync(async () =>
        {
            var created = await api.CreateLeagueAsync(new CreateLeagueRequest(NewLeagueName.Trim(), SelectedSport.Id));
            Leagues.Insert(0, created);
            NewLeagueName = string.Empty;
            ShowCreatePanel = false;
        });
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
        });
    }

    [RelayCommand]
    private async Task EndLeagueAsync(LeagueDto league)
    {
        if (league is null || !league.CanEnd) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "End league?",
            "This league will move to Completed leagues in History. This cannot be undone.",
            "End league", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () =>
        {
            await api.EndLeagueAsync(league.Id);
            Leagues.Remove(league);
        });
    }
}
