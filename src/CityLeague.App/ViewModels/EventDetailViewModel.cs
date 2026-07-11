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



    public ObservableCollection<ParticipantDto> Participants { get; } = [];

    public ObservableCollection<SelectableContact> InviteCandidates { get; } = [];



    [ObservableProperty]

    private EventDetailDto? detail;



    [ObservableProperty]

    private IReadOnlyList<PositionDto> positions = [];



    [ObservableProperty]

    private Guid? currentUserId;



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(CanSubmitResult))]

    [NotifyPropertyChangedFor(nameof(IsCompleted))]

    [NotifyPropertyChangedFor(nameof(CanDelete))]

    [NotifyPropertyChangedFor(nameof(IsReadOnly))]

    [NotifyPropertyChangedFor(nameof(StatusText))]

    private string status = "Open";



    [ObservableProperty]

    private bool showInvitePanel;



    [ObservableProperty]

    private bool forceReadOnly;



    public string Title => Detail?.Title ?? "Event";

    public string StatusText => Status;

    public bool IsOwner => Detail?.IsOwner ?? false;

    public bool CanInvite => !IsReadOnly && (Detail?.CanInvite ?? false);

    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public bool IsReadOnly => ForceReadOnly || IsCompleted ||

        string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    public bool CanSubmitResult => IsOwner && !IsReadOnly &&

        !string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    public bool CanDelete => IsOwner && !IsCompleted &&

        !string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    public string? ResultText => Detail?.Result is { } r ? $"{r.HomeScore} - {r.AwayScore} ({r.WinningSide})" : null;



    public void ApplyQueryAttributes(IDictionary<string, object> query)

    {

        if (query.TryGetValue("eventId", out var value) && Guid.TryParse(value?.ToString(), out var id))

            _eventId = id;

        if (query.TryGetValue("readOnly", out var ro))

            ForceReadOnly = bool.TryParse(ro?.ToString(), out var readOnly) && readOnly;

    }



    [RelayCommand]

    private async Task AppearingAsync()

    {

        CurrentUserId = auth.CurrentUser?.Id;

        await LoadAsync();

        if (!IsReadOnly)

            await ConnectHubAsync();

    }



    [RelayCommand]

    private async Task LoadAsync()

    {

        await RunAsync(async () =>

        {

            var detail = await api.GetEventAsync(_eventId);

            Detail = detail;

            Status = detail.Status;

            Positions = detail.Positions;



            Participants.Clear();

            foreach (var p in detail.Participants)

                Participants.Add(p);



            OnPropertyChanged(nameof(Title));

            OnPropertyChanged(nameof(IsOwner));

            OnPropertyChanged(nameof(CanInvite));

            OnPropertyChanged(nameof(CanDelete));

            OnPropertyChanged(nameof(IsReadOnly));

            OnPropertyChanged(nameof(ResultText));



            if (!IsReadOnly)

                await LoadInviteCandidatesAsync();

        });

    }



    private async Task LoadInviteCandidatesAsync()

    {

        if (!CanInvite) return;

        try

        {

            var contacts = await api.GetContactsAsync();

            var participantIds = Participants.Select(p => p.UserId).ToHashSet();

            InviteCandidates.Clear();

            foreach (var c in contacts)

            {

                if (string.Equals(c.Status, "Accepted", StringComparison.OrdinalIgnoreCase) && !participantIds.Contains(c.User.Id))

                    InviteCandidates.Add(new SelectableContact(c.User));

            }

        }

        catch

        {

            // Non-critical.

        }

    }



    private async Task ConnectHubAsync()

    {

        if (_hubStarted || IsReadOnly) return;

        try

        {

            hub.PositionChanged += OnPositionChanged;

            hub.ParticipantJoined += OnParticipantJoined;

            hub.EventCompleted += OnEventCompleted;

            await hub.StartAsync(_eventId);

            _hubStarted = true;

        }

        catch

        {

            // Live updates unavailable; the screen still works via manual refresh.

        }

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

        if (IsReadOnly) return;

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

    private void ToggleInvitePanel() => ShowInvitePanel = !ShowInvitePanel;



    [RelayCommand]

    private async Task InviteSelectedAsync()

    {

        var ids = InviteCandidates.Where(c => c.IsSelected).Select(c => c.User.Id).ToList();

        if (ids.Count == 0) return;



        await RunAsync(async () =>

        {

            await api.InviteAsync(_eventId, ids);

            foreach (var id in ids)

            {

                var candidate = InviteCandidates.FirstOrDefault(c => c.User.Id == id);

                if (candidate is not null)

                    InviteCandidates.Remove(candidate);

            }

            ShowInvitePanel = false;

        });

    }



    [RelayCommand]

    private async Task SubmitResultAsync()

        => await Shell.Current.GoToAsync($"{AppRoutes.SubmitResult}?eventId={_eventId}");



    [RelayCommand]

    private async Task DeleteAsync()

    {

        if (!CanDelete) return;



        var hasOtherUsers = Participants.Count > 1 || Positions.Any(p => p.UserId.HasValue);

        var message = hasOtherUsers

            ? "Players have already joined or claimed positions. Are you sure you want to delete this match?"

            : "This match will be permanently deleted.";



        var confirmed = await Shell.Current.DisplayAlert("Delete match?", message, "Delete", "Cancel");

        if (!confirmed) return;



        await RunAsync(async () =>

        {

            await api.DeleteEventAsync(_eventId);

            await Shell.Current.GoToAsync("..");

        });

    }



    private void OnPositionChanged(PositionChangedDto change) => MainThread.BeginInvokeOnMainThread(() =>

    {

        var updated = Positions.Select(p => p.SlotId == change.SlotId

            ? p with { UserId = change.UserId, UserHandle = change.UserHandle, UserDisplayName = change.UserDisplayName, UserAvatarUrl = change.UserAvatarUrl }

            : p).ToList();

        Positions = updated;

    });



    private void OnParticipantJoined(ParticipantDto participant) => MainThread.BeginInvokeOnMainThread(() =>

    {

        if (Participants.All(p => p.UserId != participant.UserId))

            Participants.Add(participant);

    });



    private void OnEventCompleted(ResultDto result) => MainThread.BeginInvokeOnMainThread(() =>

    {

        Status = "Completed";

        if (Detail is not null)

            Detail = Detail with { Status = "Completed", Result = result };

        OnPropertyChanged(nameof(ResultText));

        OnPropertyChanged(nameof(IsReadOnly));

        OnPropertyChanged(nameof(CanDelete));

    });



    public void Dispose()

    {

        hub.PositionChanged -= OnPositionChanged;

        hub.ParticipantJoined -= OnParticipantJoined;

        hub.EventCompleted -= OnEventCompleted;

    }

}


