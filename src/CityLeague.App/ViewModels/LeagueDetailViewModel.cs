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
    private readonly List<SelectableContact> _allInviteCandidates = [];

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
    public ObservableCollection<SelectableContact> InviteCandidates { get; } = [];

    [ObservableProperty]
    private LeagueDetailDto? league;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool showAddPeople;

    [ObservableProperty]
    private Guid? myUserId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilteredInviteCandidates))]
    [NotifyPropertyChangedFor(nameof(ShowInviteSearchEmpty))]
    private string inviteSearchQuery = string.Empty;

    public bool ShowEmptyResults => MatchResults.Count == 0;
    public bool ShowEmptyParticipants => Participants.Count == 0;
    public bool HasInviteCandidates => _allInviteCandidates.Count > 0;
    public bool HasFilteredInviteCandidates => InviteCandidates.Count > 0;
    public bool ShowInviteSearchEmpty => ShowAddPeople && HasInviteCandidates && !HasFilteredInviteCandidates;
    public bool CanManageTeams => League is { CanUploadLogo: true };
    public bool CanAssignLeaders => League is { HasStarted: false } && (League.IsOwner || League.IsTeamLeader);

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

    public LeagueTeamDto? TeamA => Teams.FirstOrDefault(t => t.SortOrder == 0);
    public LeagueTeamDto? TeamB => Teams.FirstOrDefault(t => t.SortOrder == 1);

    partial void OnInviteSearchQueryChanged(string value) => ApplyInviteFilter();

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
            ApplyDetail(await api.GetLeagueAsync(_leagueId));
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
        await RunAsync(LoadInviteCandidatesAsync);
    }

    [RelayCommand]
    private async Task InviteSelectedAsync()
    {
        if (League is null || !League.CanAddParticipants) return;
        var ids = _allInviteCandidates.Where(c => c.IsSelected).Select(c => c.User.Id).ToList();
        if (ids.Count == 0)
        {
            ErrorMessage = "Select at least one contact.";
            return;
        }

        await RunAsync(async () =>
        {
            ApplyDetail(await api.AddLeagueParticipantsAsync(_leagueId, ids));
            ShowAddPeople = false;
            await LoadInviteCandidatesAsync();
        });
    }

    [RelayCommand]
    private async Task MoveParticipantToTeamAAsync(LeagueParticipantDto participant)
    {
        if (TeamA is null) return;
        await MoveAsync(participant, TeamA.Id);
    }

    [RelayCommand]
    private async Task MoveParticipantToTeamBAsync(LeagueParticipantDto participant)
    {
        if (TeamB is null) return;
        await MoveAsync(participant, TeamB.Id);
    }

    [RelayCommand]
    private async Task UnassignParticipantAsync(LeagueParticipantDto participant)
        => await MoveAsync(participant, null);

    [RelayCommand]
    private async Task MakeLeaderOfTeamAAsync(LeagueParticipantDto participant)
    {
        if (TeamA is null) return;
        await AssignLeaderInternalAsync(TeamA, participant.UserId);
    }

    [RelayCommand]
    private async Task MakeLeaderOfTeamBAsync(LeagueParticipantDto participant)
    {
        if (TeamB is null) return;
        await AssignLeaderInternalAsync(TeamB, participant.UserId);
    }

    [RelayCommand]
    private async Task RenameTeamAsync(LeagueTeamDto team)
    {
        if (team is null || League is null || !CanManageTeams) return;
        var name = await Shell.Current.DisplayPromptAsync(
            "Team name",
            $"Rename {team.Name}",
            "Save",
            "Cancel",
            maxLength: 40,
            keyboard: Keyboard.Text,
            initialValue: team.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == team.Name) return;

        await RunAsync(async () =>
            ApplyDetail(await api.RenameLeagueTeamAsync(_leagueId, team.Id, name.Trim())));
    }

    [RelayCommand]
    private async Task PickLeaderAsync(LeagueTeamDto team)
    {
        if (team is null || !CanAssignLeaders) return;
        var candidates = Participants.Where(p => !p.IsLeader || p.UserId == team.LeaderUserId).ToList();
        if (candidates.Count == 0)
        {
            ErrorMessage = "Add people before assigning a leader.";
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet(
            $"Leader for {team.Name}", "Cancel", null,
            candidates.Select(c => c.DisplayName).ToArray());
        if (choice is null or "Cancel") return;
        var person = candidates.FirstOrDefault(c => c.DisplayName == choice);
        if (person is null) return;
        await AssignLeaderInternalAsync(team, person.UserId);
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

    private async Task MoveAsync(LeagueParticipantDto? participant, Guid? teamId)
    {
        if (participant is null || League is null || !participant.CanChangeTeam) return;
        if (MyUserId is Guid me && participant.UserId != me && !League.IsOwner && !League.IsTeamLeader)
            return;

        await RunAsync(async () =>
            ApplyDetail(await api.MoveLeagueParticipantAsync(_leagueId, participant.UserId, teamId)));
    }

    private async Task AssignLeaderInternalAsync(LeagueTeamDto team, Guid userId)
    {
        if (!CanAssignLeaders) return;
        await RunAsync(async () =>
            ApplyDetail(await api.SetLeagueTeamLeaderAsync(_leagueId, team.Id, userId)));
    }

    private async Task LoadInviteCandidatesAsync()
    {
        var contacts = await api.GetContactsAsync();
        var inLeague = Participants.Select(p => p.UserId).ToHashSet();
        _allInviteCandidates.Clear();
        foreach (var c in contacts.Where(c =>
                     string.Equals(c.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
                     && !inLeague.Contains(c.User.Id)))
            _allInviteCandidates.Add(new SelectableContact(c.User));

        InviteSearchQuery = string.Empty;
        ApplyInviteFilter();
        OnPropertyChanged(nameof(HasInviteCandidates));
        OnPropertyChanged(nameof(ShowInviteSearchEmpty));
    }

    private void ApplyInviteFilter()
    {
        InviteCandidates.Clear();
        var q = InviteSearchQuery?.Trim() ?? string.Empty;
        IEnumerable<SelectableContact> source = _allInviteCandidates;
        if (!string.IsNullOrEmpty(q))
        {
            source = source.Where(c =>
                c.User.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (c.User.Handle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (var c in source)
            InviteCandidates.Add(c);

        OnPropertyChanged(nameof(HasFilteredInviteCandidates));
        OnPropertyChanged(nameof(ShowInviteSearchEmpty));
    }

    private void ApplyDetail(LeagueDetailDto detail)
    {
        League = detail;
        Teams.Clear();
        foreach (var t in detail.Teams.OrderBy(t => t.SortOrder))
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
        OnPropertyChanged(nameof(CanManageTeams));
        OnPropertyChanged(nameof(CanAssignLeaders));
        OnPropertyChanged(nameof(TeamA));
        OnPropertyChanged(nameof(TeamB));
    }
}
