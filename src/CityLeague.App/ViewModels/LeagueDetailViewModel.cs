using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

[QueryProperty(nameof(LeagueId), "leagueId")]
public partial class LeagueDetailViewModel(ICityLeagueApi api) : BaseViewModel
{
    private Guid _leagueId;

    public string LeagueId
    {
        get => _leagueId.ToString();
        set
        {
            if (Guid.TryParse(value, out var id))
                _leagueId = id;
        }
    }

    public ObservableCollection<LeagueTeamDto> Teams { get; } = [];
    public ObservableCollection<LeagueParticipantDto> Participants { get; } = [];
    public ObservableCollection<LeagueMatchResultDto> MatchResults { get; } = [];
    public ObservableCollection<ContactDto> AddCandidates { get; } = [];

    [ObservableProperty]
    private LeagueDetailDto? league;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool showAddPeople;

    [ObservableProperty]
    private Guid? myUserId;

    public bool ShowEmptyResults => MatchResults.Count == 0;
    public bool ShowEmptyParticipants => Participants.Count == 0;
    public bool HasAddCandidates => AddCandidates.Count > 0;

    public string ProgressLabel => League is null
        ? string.Empty
        : $"{League.CompletedMatchCount} / {League.PlannedMatchCount} matches";

    public string StatusSubtitle => League is null
        ? string.Empty
        : League.Status switch
        {
            "Draft" => "Set leaders, logos, and roster — then start",
            "Finished" => "This league is finished",
            _ => "Season in progress",
        };

    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (_leagueId == Guid.Empty) return;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var me = await api.GetMeAsync();
            MyUserId = me.Id;
            var detail = await api.GetLeagueAsync(_leagueId);
            ApplyDetail(detail);
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
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task StartAsync()
    {
        if (League is null || !League.CanStart) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Start league?",
            "Team leaders will be locked to their teams. You can still add people and move non-leaders.",
            "Start", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () => ApplyDetail(await api.StartLeagueAsync(_leagueId)));
    }

    [RelayCommand]
    private async Task ExtendAsync()
    {
        if (League is null || !League.CanExtend) return;
        var choice = await Shell.Current.DisplayActionSheet(
            "Extend league by…", "Cancel", null, "+1 match", "+3 matches", "+5 matches");
        var add = choice switch
        {
            "+1 match" => 1,
            "+3 matches" => 3,
            "+5 matches" => 5,
            _ => 0,
        };
        if (add == 0) return;
        await RunAsync(async () => ApplyDetail(await api.ExtendLeagueAsync(_leagueId, add)));
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (League is null || !League.CanEnd) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Finish league?",
            "End the season early and move it to History.",
            "Finish", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () =>
        {
            ApplyDetail(await api.EndLeagueAsync(_leagueId));
            await Shell.Current.DisplayAlert("League finished", "You'll find it under History → Completed leagues.", "OK");
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (League is null || !League.CanDelete) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Delete league?",
            "This removes the league permanently.",
            "Delete", "Cancel");
        if (!confirmed) return;

        await RunAsync(async () =>
        {
            await api.DeleteLeagueAsync(_leagueId);
            await Shell.Current.GoToAsync("..");
        });
    }

    [RelayCommand]
    private async Task ToggleAddPeopleAsync()
    {
        ShowAddPeople = !ShowAddPeople;
        if (!ShowAddPeople) return;

        await RunAsync(async () =>
        {
            var contacts = await api.GetContactsAsync();
            var inLeague = Participants.Select(p => p.UserId).ToHashSet();
            AddCandidates.Clear();
            foreach (var c in contacts.Where(c =>
                         string.Equals(c.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
                         && !inLeague.Contains(c.User.Id)))
                AddCandidates.Add(c);
            OnPropertyChanged(nameof(HasAddCandidates));
        });
    }

    [RelayCommand]
    private async Task AddPersonAsync(ContactDto contact)
    {
        if (contact is null || League is null || !League.CanAddParticipants) return;
        await RunAsync(async () =>
        {
            ApplyDetail(await api.AddLeagueParticipantsAsync(_leagueId, [contact.User.Id]));
            AddCandidates.Remove(contact);
            OnPropertyChanged(nameof(HasAddCandidates));
        });
    }

    [RelayCommand]
    private async Task MoveToTeamAsync(LeagueParticipantDto participant)
    {
        if (participant is null || League is null || !participant.CanChangeTeam) return;
        if (MyUserId is Guid me && participant.UserId != me && !League.IsOwner && !League.IsTeamLeader)
            return;

        var teams = Teams.ToList();
        if (teams.Count == 0) return;

        var options = teams.Select(t => t.Name).Append("Unassigned").ToArray();
        var choice = await Shell.Current.DisplayActionSheet(
            $"Move {participant.DisplayName}", "Cancel", null, options);
        if (choice is null or "Cancel") return;

        Guid? teamId = null;
        if (choice != "Unassigned")
        {
            var team = teams.FirstOrDefault(t => t.Name == choice);
            if (team is null) return;
            teamId = team.Id;
        }

        await RunAsync(async () =>
            ApplyDetail(await api.MoveLeagueParticipantAsync(_leagueId, participant.UserId, teamId)));
    }

    [RelayCommand]
    private async Task SetLeaderAsync(LeagueTeamDto team)
    {
        if (team is null || League is null || League.HasStarted) return;
        if (!League.IsOwner && !League.IsTeamLeader) return;

        var candidates = Participants.Where(p => !p.IsLeader || p.UserId == team.LeaderUserId).ToList();
        if (candidates.Count == 0)
        {
            ErrorMessage = "Add people to the league before assigning a leader.";
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet(
            $"Leader for {team.Name}", "Cancel", null,
            candidates.Select(c => c.DisplayName).ToArray());
        if (choice is null or "Cancel") return;

        var person = candidates.FirstOrDefault(c => c.DisplayName == choice);
        if (person is null) return;

        await RunAsync(async () =>
            ApplyDetail(await api.SetLeagueTeamLeaderAsync(_leagueId, team.Id, person.UserId)));
    }

    [RelayCommand]
    private async Task UploadLogoAsync(LeagueTeamDto team)
    {
        if (team is null || League is null || !League.CanUploadLogo) return;

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Logo for {team.Name}",
            FileTypes = FilePickerFileType.Images,
        });
        if (result is null) return;

        await RunAsync(async () =>
        {
            await using var stream = await result.OpenReadAsync();
            var contentType = string.IsNullOrWhiteSpace(result.ContentType) ? "image/png" : result.ContentType;
            ApplyDetail(await api.UploadLeagueTeamLogoAsync(
                _leagueId, team.Id, stream, result.FileName, contentType));
        });
    }

    private void ApplyDetail(LeagueDetailDto detail)
    {
        League = detail;
        Teams.Clear();
        foreach (var t in detail.Teams)
            Teams.Add(t);
        Participants.Clear();
        foreach (var p in detail.Participants)
            Participants.Add(p);
        MatchResults.Clear();
        foreach (var m in detail.MatchResults)
            MatchResults.Add(m);

        OnPropertyChanged(nameof(ShowEmptyResults));
        OnPropertyChanged(nameof(ShowEmptyParticipants));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StatusSubtitle));
    }
}
