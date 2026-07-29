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

public partial class SelectableFormat(EventFormatDto format) : ObservableObject
{
    public EventFormatDto Format { get; } = format;
    public string Name => Format.Name;
    public string ShortLabel => $"{Format.PlayersPerSide}v{Format.PlayersPerSide}";

    [ObservableProperty]
    private bool isSelected;
}

public partial class CreateEventViewModel(ICityLeagueApi api, IOsmFootballFieldService fields)
    : BaseViewModel, IQueryAttributable
{
    private string? _pendingSportKey;
    private bool _suppressLocationSuggestions;
    private CancellationTokenSource? _suggestCts;

    public ObservableCollection<SportDto> Sports { get; } = [];
    public ObservableCollection<SelectableFormat> FormatChoices { get; } = [];
    public ObservableCollection<SeriesDto> Series { get; } = [];
    public ObservableCollection<FootballField> LocationSuggestions { get; } = [];

    [ObservableProperty]
    private SportDto? selectedSport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private EventFormatDto? selectedFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private string title = string.Empty;

    [ObservableProperty]
    private DateTime date = DateTime.Now.Date.AddDays(1);

    [ObservableProperty]
    private TimeSpan time = new(18, 0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocationSuggestions))]
    private string? location;

    [ObservableProperty]
    private SeriesDto? selectedSeries;

    [ObservableProperty]
    private string? newSeriesName;

    [ObservableProperty]
    private bool showSeriesOptions;

    [ObservableProperty]
    private string locationHint = "Type a pitch in your city";

    public bool HasLocationSuggestions => LocationSuggestions.Count > 0;

    public bool CanCreate => SelectedSport is not null
        && SelectedFormat is not null
        && !string.IsNullOrWhiteSpace(Title)
        && IsNotBusy;

    public string WhenSummary
    {
        get
        {
            var local = Date.Date.Add(Time);
            var today = DateTime.Today;
            var day = local.Date == today
                ? "Today"
                : local.Date == today.AddDays(1)
                    ? "Tomorrow"
                    : local.ToString("ddd d MMM");
            return $"{day} · {local:HH:mm}";
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("location", out var value) && value is string loc && !string.IsNullOrWhiteSpace(loc))
        {
            _suppressLocationSuggestions = true;
            Location = loc;
            LocationSuggestions.Clear();
            OnPropertyChanged(nameof(HasLocationSuggestions));
            _suppressLocationSuggestions = false;
        }

        if (query.TryGetValue("sport", out var sport) && sport is string sportKey && !string.IsNullOrWhiteSpace(sportKey))
            _pendingSportKey = sportKey.Trim().ToLowerInvariant();
    }

    partial void OnSelectedSportChanged(SportDto? value)
    {
        if (value is null) return;
        LoadFormatsForSport(value);
        OnPropertyChanged(nameof(CanCreate));
    }

    partial void OnDateChanged(DateTime value) => OnPropertyChanged(nameof(WhenSummary));
    partial void OnTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(WhenSummary));

    partial void OnLocationChanged(string? value)
    {
        if (_suppressLocationSuggestions)
            return;
        _ = RefreshLocationSuggestionsAsync(value);
    }

    protected override void OnBusyStateChanged(bool isBusy)
        => OnPropertyChanged(nameof(CanCreate));

    [RelayCommand]
    private void ToggleSeriesOptions() => ShowSeriesOptions = !ShowSeriesOptions;

    [RelayCommand]
    private async Task PickLocationAsync()
        => await Shell.Current.GoToAsync(AppRoutes.LocationPicker);

    [RelayCommand]
    private void SelectLocationSuggestion(FootballField field)
    {
        if (field is null) return;
        _suppressLocationSuggestions = true;
        Location = field.DisplayLabel;
        LocationSuggestions.Clear();
        OnPropertyChanged(nameof(HasLocationSuggestions));
        _suppressLocationSuggestions = false;
    }

    [RelayCommand]
    private Task SelectFormatAsync(SelectableFormat choice)
    {
        foreach (var item in FormatChoices)
            item.IsSelected = ReferenceEquals(item, choice);

        SelectedFormat = choice.Format;
        if (string.IsNullOrWhiteSpace(Title))
            Title = $"{choice.ShortLabel} · {WhenSummary}";

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        OnPropertyChanged(nameof(WhenSummary));
        _ = EnsureCityFieldsAsync();

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

    private async Task EnsureCityFieldsAsync()
    {
        try
        {
            if (fields.CachedCityFields.Count == 0)
                await fields.PrefetchForCurrentCityAsync();

            var city = fields.CachedCityName;
            LocationHint = string.IsNullOrWhiteSpace(city)
                ? "Type a pitch in your city"
                : $"Pitches in {city} · OpenStreetMap";

            if (!string.IsNullOrWhiteSpace(Location))
                await RefreshLocationSuggestionsAsync(Location);
        }
        catch
        {
            LocationHint = "Type a pitch name";
        }
    }

    private async Task RefreshLocationSuggestionsAsync(string? query)
    {
        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var ct = _suggestCts.Token;

        try
        {
            await Task.Delay(180, ct);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 1)
        {
            LocationSuggestions.Clear();
            OnPropertyChanged(nameof(HasLocationSuggestions));
            return;
        }

        if (fields.CachedCityFields.Count == 0)
        {
            try { await fields.PrefetchForCurrentCityAsync(ct); }
            catch { /* keep empty */ }
        }

        if (ct.IsCancellationRequested)
            return;

        var matches = fields.CachedCityFields
            .Where(f => f.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || f.DisplayLabel.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        LocationSuggestions.Clear();
        foreach (var m in matches)
            LocationSuggestions.Add(m);
        OnPropertyChanged(nameof(HasLocationSuggestions));
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
        FormatChoices.Clear();
        foreach (var format in sport.Formats)
            FormatChoices.Add(new SelectableFormat(format));

        var preferred = FormatChoices.FirstOrDefault(f => f.Format.PlayersPerSide == 7)
                        ?? FormatChoices.FirstOrDefault();
        if (preferred is not null)
        {
            preferred.IsSelected = true;
            SelectedFormat = preferred.Format;
        }
        else
        {
            SelectedFormat = null;
        }
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
            ShowSeriesOptions = false;
            LocationSuggestions.Clear();
            OnPropertyChanged(nameof(HasLocationSuggestions));

            await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={createdEvent.Id}");
        });
    }
}
