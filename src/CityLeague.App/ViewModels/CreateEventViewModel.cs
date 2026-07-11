using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class SelectableContact(UserDto user) : ObservableObject
{
    public UserDto User { get; } = user;

    [ObservableProperty]
    private bool isSelected;
}

public partial class CreateEventViewModel(ICityLeagueApi api) : BaseViewModel, IQueryAttributable
{
    private string? _pendingSportKey;

    public ObservableCollection<SportDto> Sports { get; } = [];
    public ObservableCollection<EventFormatDto> Formats { get; } = [];
    public ObservableCollection<SeriesDto> Series { get; } = [];

    [ObservableProperty]
    private SportDto? selectedSport;

    [ObservableProperty]
    private EventFormatDto? selectedFormat;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private DateTime date = DateTime.Now.Date.AddDays(1);

    [ObservableProperty]
    private TimeSpan time = new(18, 0, 0);

    [ObservableProperty]
    private string? location;

    [ObservableProperty]
    private SeriesDto? selectedSeries;

    [ObservableProperty]
    private string? newSeriesName;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("location", out var value) && value is string loc && !string.IsNullOrWhiteSpace(loc))
            Location = loc;

        if (query.TryGetValue("sport", out var sport) && sport is string sportKey && !string.IsNullOrWhiteSpace(sportKey))
            _pendingSportKey = sportKey.Trim().ToLowerInvariant();
    }

    partial void OnSelectedSportChanged(SportDto? value)
    {
        if (value is null) return;
        LoadFormatsForSport(value);
    }

    [RelayCommand]
    private async Task PickLocationAsync()
        => await Shell.Current.GoToAsync(AppRoutes.LocationPicker);

    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (Sports.Count > 0)
        {
            ApplyPendingSport();
            return;
        }

        await RunAsync(async () =>
        {
            var sports = await api.GetSportsAsync();
            Sports.Clear();
            foreach (var sport in sports.Where(s =>
                         string.Equals(s.Availability, "Enabled", StringComparison.OrdinalIgnoreCase)))
                Sports.Add(sport);

            ApplyPendingSport();

            Series.Clear();
            foreach (var s in await api.GetSeriesAsync())
                Series.Add(s);
        });
    }

    private void ApplyPendingSport()
    {
        if (Sports.Count == 0) return;

        SelectedSport = !string.IsNullOrEmpty(_pendingSportKey)
            ? Sports.FirstOrDefault(s => string.Equals(s.Key, _pendingSportKey, StringComparison.OrdinalIgnoreCase))
            : null;

        SelectedSport ??= Sports.FirstOrDefault(s =>
                            string.Equals(s.Key, "football", StringComparison.OrdinalIgnoreCase))
                        ?? Sports.FirstOrDefault();

        _pendingSportKey = null;
    }

    private void LoadFormatsForSport(SportDto sport)
    {
        Formats.Clear();
        foreach (var format in sport.Formats)
            Formats.Add(format);

        SelectedFormat = Formats.FirstOrDefault(f => f.PlayersPerSide == 7) ?? Formats.FirstOrDefault();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (SelectedSport is null)
        {
            ErrorMessage = "Choose a sport.";
            return;
        }

        if (SelectedFormat is null)
        {
            ErrorMessage = "Choose a format.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Enter a title.";
            return;
        }

        await RunAsync(async () =>
        {
            var seriesId = SelectedSeries?.Id;
            if (seriesId is null && !string.IsNullOrWhiteSpace(NewSeriesName))
            {
                var created = await api.CreateSeriesAsync(new CreateSeriesRequest(NewSeriesName.Trim(), SelectedSport.Id));
                Series.Add(created);
                seriesId = created.Id;
            }

            var scheduled = new DateTimeOffset(Date.Date.Add(Time), TimeZoneInfo.Local.GetUtcOffset(Date.Date.Add(Time)));

            var request = new CreateEventRequest(
                SelectedFormat.Id,
                Title.Trim(),
                scheduled,
                string.IsNullOrWhiteSpace(Location) ? null : Location!.Trim(),
                seriesId,
                []);

            var createdEvent = await api.CreateEventAsync(request);

            Title = string.Empty;
            Location = null;
            NewSeriesName = null;
            SelectedSeries = null;

            await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={createdEvent.Id}");
        });
    }
}
