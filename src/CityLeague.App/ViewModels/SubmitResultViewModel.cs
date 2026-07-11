using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class SubmitResultViewModel(ICityLeagueApi api) : BaseViewModel, IQueryAttributable
{
    private Guid _eventId;

    [ObservableProperty]
    private string eventTitle = "Submit result";

    [ObservableProperty]
    private int homeScore;

    [ObservableProperty]
    private int awayScore;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("eventId", out var value) && Guid.TryParse(value?.ToString(), out var id))
            _eventId = id;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await RunAsync(async () =>
        {
            var detail = await api.GetEventAsync(_eventId);
            EventTitle = detail.Title;
        });
    }

    [RelayCommand]
    private void IncrementHome() => HomeScore++;

    [RelayCommand]
    private void DecrementHome() => HomeScore = Math.Max(0, HomeScore - 1);

    [RelayCommand]
    private void IncrementAway() => AwayScore++;

    [RelayCommand]
    private void DecrementAway() => AwayScore = Math.Max(0, AwayScore - 1);

    [RelayCommand]
    private async Task SubmitAsync()
    {
        await RunAsync(async () =>
        {
            await api.SubmitResultAsync(_eventId, new SubmitResultRequest(HomeScore, AwayScore));
            await Shell.Current.GoToAsync("..");
        });
    }
}
