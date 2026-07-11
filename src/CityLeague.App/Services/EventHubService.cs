using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace CityLeague.App.Services;

/// <summary>Wraps a SignalR connection to the event hub for a single event's live updates.</summary>
public interface IEventHubService : IAsyncDisposable
{
    event Action<PositionChangedDto>? PositionChanged;
    event Action<ParticipantDto>? ParticipantJoined;
    event Action<ResultDto>? EventCompleted;

    Task StartAsync(Guid eventId);
    Task StopAsync();
}

public class EventHubService(ApiSettings settings, ITokenStore tokens) : IEventHubService
{
    private HubConnection? _connection;

    public event Action<PositionChangedDto>? PositionChanged;
    public event Action<ParticipantDto>? ParticipantJoined;
    public event Action<ResultDto>? EventCompleted;

    public async Task StartAsync(Guid eventId)
    {
        await StopAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(settings.HubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(tokens.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<PositionChangedDto>("PositionChanged", dto => PositionChanged?.Invoke(dto));
        _connection.On<ParticipantDto>("ParticipantJoined", dto => ParticipantJoined?.Invoke(dto));
        _connection.On<ResultDto>("EventCompleted", dto => EventCompleted?.Invoke(dto));

        _connection.Reconnected += async _ =>
        {
            try { await _connection.InvokeAsync("JoinEvent", eventId); }
            catch { /* best effort re-join */ }
        };

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinEvent", eventId);
    }

    public async Task StopAsync()
    {
        if (_connection is null)
            return;
        try
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
        catch
        {
            // ignore shutdown errors
        }
        finally
        {
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
