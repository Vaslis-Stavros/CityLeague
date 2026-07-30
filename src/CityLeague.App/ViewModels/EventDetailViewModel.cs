using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class EventDetailViewModel(ICityLeagueApi api, IAuthService auth, IEventHubService hub)
    : BaseViewModel, IQueryAttributable, IDisposable
{
    private Guid _eventId;
    private bool _hubStarted;
    private readonly List<SelectableContact> _allInviteCandidates = [];

    public ObservableCollection<ParticipantDto> Participants { get; } = [];
    public ObservableCollection<SelectableContact> InviteCandidates { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(ScheduleLabel))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsOwner))]
    [NotifyPropertyChangedFor(nameof(CanInvite))]
    [NotifyPropertyChangedFor(nameof(CanSubmitResult))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(CanLock))]
    [NotifyPropertyChangedFor(nameof(CanUnlock))]
    [NotifyPropertyChangedFor(nameof(CanEditSchedule))]
    [NotifyPropertyChangedFor(nameof(CanLeave))]
    [NotifyPropertyChangedFor(nameof(IsPendingResult))]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(PitchReadOnly))]
    [NotifyPropertyChangedFor(nameof(ResultText))]
    [NotifyPropertyChangedFor(nameof(HintText))]
    private EventDetailDto? detail;

    [ObservableProperty]
    private IReadOnlyList<PositionDto> positions = [];

    [ObservableProperty]
    private Guid? currentUserId;

    [ObservableProperty]
    private string status = "Open";

    [ObservableProperty]
    private bool showInvitePanel;

    [ObservableProperty]
    private bool showScheduleEditor;

    [ObservableProperty]
    private bool forceReadOnly;

    [ObservableProperty]
    private string inviteSearchQuery = string.Empty;

    [ObservableProperty]
    private DateTime editDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan editTime = TimeSpan.FromHours(18);

    public string Title => Detail?.Title ?? "Event";
    public string StatusText => Detail?.Status ?? Status;
    public string ScheduleLabel
    {
        get
        {
            if (Detail is null) return string.Empty;
            var local = Detail.ScheduledAt.ToLocalTime();
            return $"{local:ddd d MMM} · {local:HH:mm}";
        }
    }

    public bool IsOwner => Detail?.IsOwner ?? false;
    public bool CanInvite => !ForceReadOnly && (Detail?.CanInvite ?? false);
    public bool IsCompleted => string.Equals(Detail?.Status, "Completed", StringComparison.OrdinalIgnoreCase);
    public bool IsReadOnly => ForceReadOnly || IsCompleted;
    public bool PitchReadOnly => IsReadOnly || string.Equals(Detail?.Status, "Incomplete", StringComparison.OrdinalIgnoreCase);
    public bool CanSubmitResult => !ForceReadOnly && (Detail?.CanSubmitResult ?? false);
    public bool CanDelete => !ForceReadOnly && (Detail?.CanDelete ?? false);
    public bool CanLock => !ForceReadOnly && (Detail?.CanLock ?? false);
    public bool CanUnlock => !ForceReadOnly && (Detail?.CanUnlock ?? false);
    public bool CanEditSchedule => !ForceReadOnly && (Detail?.CanEditSchedule ?? false);
    public bool CanLeave => !ForceReadOnly && (Detail?.CanLeave ?? false);
    public bool IsPendingResult => Detail?.IsPendingResult ?? false;
    public string? ResultText => Detail?.Result is { } r ? $"{r.HomeScore} - {r.AwayScore} ({r.WinningSide})" : null;

    public string HintText => Detail?.Status switch
    {
        "Locked" when Detail.IsPast => "Kickoff passed — submit the result to finish this match.",
        "Locked" => "Roster locked. Players can still swap positions.",
        "Incomplete" => "Kickoff passed without a lock. Reschedule or delete.",
        "Completed" => "Final lineup and result.",
        _ => "Tap an open spot to claim it — tap yours to leave.",
    };

    public bool HasInviteCandidates => _allInviteCandidates.Count > 0;
    public bool HasFilteredInviteCandidates => InviteCandidates.Count > 0;
    public bool ShowInviteSearchEmpty => HasInviteCandidates && !HasFilteredInviteCandidates;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("eventId", out var value) && Guid.TryParse(value?.ToString(), out var id))
            _eventId = id;
        if (query.TryGetValue("readOnly", out var ro))
            ForceReadOnly = bool.TryParse(ro?.ToString(), out var readOnly) && readOnly;
    }

    partial void OnInviteSearchQueryChanged(string value) => ApplyInviteFilter();

    [RelayCommand]
    private async Task AppearingAsync()
    {
        CurrentUserId = auth.CurrentUser?.Id;
        await LoadAsync();
        if (!PitchReadOnly)
            await ConnectHubAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            ApplyDetail(await api.GetEventAsync(_eventId));
            if (CanInvite)
                await LoadInviteCandidatesAsync();
        });
    }

    private void ApplyDetail(EventDetailDto detail)
    {
        Detail = detail;
        Status = detail.Status;
        Positions = detail.Positions;
        var local = detail.ScheduledAt.ToLocalTime();
        EditDate = local.Date;
        EditTime = local.TimeOfDay;

        Participants.Clear();
        foreach (var p in detail.Participants)
            Participants.Add(p);
    }

    private async Task LoadInviteCandidatesAsync()
    {
        if (!CanInvite) return;
        try
        {
            var contacts = await api.GetContactsAsync();
            var participantIds = Participants.Select(p => p.UserId).ToHashSet();
            _allInviteCandidates.Clear();
            foreach (var c in contacts)
            {
                if (string.Equals(c.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
                    && !participantIds.Contains(c.User.Id))
                    _allInviteCandidates.Add(new SelectableContact(c.User));
            }
            InviteSearchQuery = string.Empty;
            ApplyInviteFilter();
            OnPropertyChanged(nameof(HasInviteCandidates));
        }
        catch { /* non-critical */ }
    }

    private void ApplyInviteFilter()
    {
        var q = InviteSearchQuery?.Trim() ?? string.Empty;
        IEnumerable<SelectableContact> filtered = _allInviteCandidates;
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = _allInviteCandidates.Where(c =>
                (c.User.DisplayName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.User.Handle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        InviteCandidates.Clear();
        foreach (var c in filtered.OrderBy(c => c.User.DisplayName))
            InviteCandidates.Add(c);
        OnPropertyChanged(nameof(HasFilteredInviteCandidates));
        OnPropertyChanged(nameof(ShowInviteSearchEmpty));
    }

    private async Task ConnectHubAsync()
    {
        if (_hubStarted || PitchReadOnly) return;
        try
        {
            hub.PositionChanged += OnPositionChanged;
            hub.ParticipantJoined += OnParticipantJoined;
            hub.EventCompleted += OnEventCompleted;
            await hub.StartAsync(_eventId);
            _hubStarted = true;
        }
        catch { /* live updates optional */ }
    }

    [RelayCommand]
    private async Task DisappearingAsync()
    {
        hub.PositionChanged -= OnPositionChanged;
        hub.ParticipantJoined -= OnParticipantJoined;
        hub.EventCompleted -= OnEventCompleted;
        await hub.StopAsync();
        _hubStarted = false;
    }

    [RelayCommand]
    private async Task SlotTappedAsync(string slotId)
    {
        if (PitchReadOnly) return;
        var slot = Positions.FirstOrDefault(p => p.SlotId == slotId);
        if (slot is null || IsCompleted) return;

        await RunAsync(async () =>
        {
            if (slot.UserId == CurrentUserId)
                await api.ReleasePositionAsync(_eventId, slotId);
            else if (slot.UserId is null)
                await api.ClaimPositionAsync(_eventId, slotId);
            if (!_hubStarted)
                await LoadAsync();
        });
    }

    [RelayCommand]
    private void ToggleInvitePanel()
    {
        ShowInvitePanel = !ShowInvitePanel;
        if (ShowInvitePanel)
        {
            InviteSearchQuery = string.Empty;
            ApplyInviteFilter();
        }
    }

    [RelayCommand]
    private void ToggleScheduleEditor()
    {
        if (!CanEditSchedule) return;
        ShowScheduleEditor = !ShowScheduleEditor;
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (!CanEditSchedule) return;
        var local = DateTime.SpecifyKind(EditDate.Date + EditTime, DateTimeKind.Local);
        await RunAsync(async () =>
        {
            ApplyDetail(await api.UpdateEventAsync(_eventId, new UpdateEventRequest(ScheduledAt: new DateTimeOffset(local))));
            ShowScheduleEditor = false;
        });
    }

    [RelayCommand]
    private async Task InviteSelectedAsync()
    {
        var ids = InviteCandidates.Where(c => c.IsSelected).Select(c => c.User.Id).ToList();
        if (ids.Count == 0) return;
        await RunAsync(async () =>
        {
            await api.InviteAsync(_eventId, ids);
            _allInviteCandidates.RemoveAll(c => ids.Contains(c.User.Id));
            ApplyInviteFilter();
            ShowInvitePanel = false;
            OnPropertyChanged(nameof(HasInviteCandidates));
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        if (!CanLock) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Lock match?",
            "The roster will freeze. Players can still swap positions. After kickoff you'll submit the result.",
            "Lock", "Cancel");
        if (!confirmed) return;
        await RunAsync(async () => ApplyDetail(await api.LockEventAsync(_eventId)));
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (!CanUnlock) return;
        await RunAsync(async () => ApplyDetail(await api.UnlockEventAsync(_eventId)));
    }

    [RelayCommand]
    private async Task SubmitResultAsync()
        => await Shell.Current.GoToAsync($"{AppRoutes.SubmitResult}?eventId={_eventId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!CanDelete) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Delete match?",
            "This match will be permanently removed for everyone.",
            "Delete", "Cancel");
        if (!confirmed) return;
        await RunAsync(async () =>
        {
            await api.DeleteEventAsync(_eventId);
            await Shell.Current.GoToAsync("..");
        });
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (!CanLeave) return;
        var confirmed = await Shell.Current.DisplayAlert(
            "Remove from your list?",
            "You'll leave this match. The organizer can still keep it for others.",
            "Remove", "Cancel");
        if (!confirmed) return;
        await RunAsync(async () =>
        {
            await api.LeaveEventAsync(_eventId);
            await Shell.Current.GoToAsync("..");
        });
    }

    private void OnPositionChanged(PositionChangedDto change) => MainThread.BeginInvokeOnMainThread(() =>
    {
        Positions = Positions.Select(p => p.SlotId == change.SlotId
            ? p with
            {
                UserId = change.UserId,
                UserHandle = change.UserHandle,
                UserDisplayName = change.UserDisplayName,
                UserAvatarUrl = change.UserAvatarUrl,
            }
            : p).ToList();
    });

    private void OnParticipantJoined(ParticipantDto participant) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Participants.All(p => p.UserId != participant.UserId))
            Participants.Add(participant);
    });

    private void OnEventCompleted(ResultDto result) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Detail is not null)
            ApplyDetail(Detail with { Status = "Completed", Result = result, CanSubmitResult = false, CanLock = false, CanUnlock = false, CanDelete = false, IsPendingResult = false });
    });

    public void Dispose()
    {
        hub.PositionChanged -= OnPositionChanged;
        hub.ParticipantJoined -= OnParticipantJoined;
        hub.EventCompleted -= OnEventCompleted;
    }
}
