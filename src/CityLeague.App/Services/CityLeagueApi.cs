using System.Net.Http.Json;
using System.Text.Json;
using CityLeague.Core.Dtos;

namespace CityLeague.App.Services;

public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface ICityLeagueApi
{
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<UserDto> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task<HandleAvailabilityDto> CheckHandleAsync(string handle, CancellationToken ct = default);
    Task<UserDto> SetHandleAsync(string handle, CancellationToken ct = default);
    Task<UserDto> UploadAvatarAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken ct = default);
    Task<ContactDto> AddContactAsync(CreateContactRequest request, CancellationToken ct = default);
    Task<ContactDto> AcceptContactAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<SportDto>> GetSportsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SeriesDto>> GetSeriesAsync(CancellationToken ct = default);
    Task<SeriesDto> CreateSeriesAsync(CreateSeriesRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummaryDto>> GetEventsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EventSummaryDto>> GetPastEventsAsync(CancellationToken ct = default);
    Task<EventDetailDto> GetEventAsync(Guid id, CancellationToken ct = default);
    Task<EventDetailDto> CreateEventAsync(CreateEventRequest request, CancellationToken ct = default);
    Task DeleteEventAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<ParticipantDto>> InviteAsync(Guid eventId, IReadOnlyList<Guid> userIds, CancellationToken ct = default);
    Task ClaimPositionAsync(Guid eventId, string slotId, CancellationToken ct = default);
    Task ReleasePositionAsync(Guid eventId, string slotId, CancellationToken ct = default);
    Task<ResultDto> SubmitResultAsync(Guid eventId, SubmitResultRequest request, CancellationToken ct = default);

    Task<MyStatsDto> GetMyStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LeagueDto>> GetCompletedLeaguesAsync(CancellationToken ct = default);
    Task<LeagueDto> CreateLeagueAsync(CreateLeagueRequest request, CancellationToken ct = default);
    Task DeleteLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<LeagueDto> EndLeagueAsync(Guid leagueId, CancellationToken ct = default);
}

public class CityLeagueApi(HttpClient http) : ICityLeagueApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<UserDto> GetMeAsync(CancellationToken ct = default)
        => GetAsync<UserDto>("/api/me", ct);

    public Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
        => SendJsonAsync<UserDto>(HttpMethod.Patch, "/api/me", request, ct);

    public Task<UserDto> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
        => SendJsonAsync<UserDto>(HttpMethod.Post, "/api/me/password", request, ct);

    public Task<HandleAvailabilityDto> CheckHandleAsync(string handle, CancellationToken ct = default)
        => GetAsync<HandleAvailabilityDto>($"/api/me/handle/available?handle={Uri.EscapeDataString(handle)}", ct);

    public Task<UserDto> SetHandleAsync(string handle, CancellationToken ct = default)
        => SendJsonAsync<UserDto>(HttpMethod.Post, "/api/me/handle", new SetHandleRequest(handle), ct);

    public async Task<UserDto> UploadAvatarAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var response = await http.PostAsync("/api/me/avatar", form, ct);
        return await ReadAsync<UserDto>(response, ct);
    }

    public async Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(string query, CancellationToken ct = default)
        => await GetAsync<List<UserSearchResultDto>>($"/api/users/search?q={Uri.EscapeDataString(query)}", ct);

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken ct = default)
        => await GetAsync<List<ContactDto>>("/api/contacts", ct);

    public Task<ContactDto> AddContactAsync(CreateContactRequest request, CancellationToken ct = default)
        => SendJsonAsync<ContactDto>(HttpMethod.Post, "/api/contacts", request, ct);

    public Task<ContactDto> AcceptContactAsync(Guid userId, CancellationToken ct = default)
        => SendJsonAsync<ContactDto>(HttpMethod.Post, $"/api/contacts/{userId}/accept", null, ct);

    public async Task<IReadOnlyList<SportDto>> GetSportsAsync(CancellationToken ct = default)
        => await GetAsync<List<SportDto>>("/api/sports", ct);

    public async Task<IReadOnlyList<SeriesDto>> GetSeriesAsync(CancellationToken ct = default)
        => await GetAsync<List<SeriesDto>>("/api/series", ct);

    public Task<SeriesDto> CreateSeriesAsync(CreateSeriesRequest request, CancellationToken ct = default)
        => SendJsonAsync<SeriesDto>(HttpMethod.Post, "/api/series", request, ct);

    public async Task<IReadOnlyList<EventSummaryDto>> GetEventsAsync(CancellationToken ct = default)
        => await GetAsync<List<EventSummaryDto>>("/api/events", ct);

    public async Task<IReadOnlyList<EventSummaryDto>> GetPastEventsAsync(CancellationToken ct = default)
        => await GetAsync<List<EventSummaryDto>>("/api/events/past", ct);

    public Task<EventDetailDto> GetEventAsync(Guid id, CancellationToken ct = default)
        => GetAsync<EventDetailDto>($"/api/events/{id}", ct);

    public Task<EventDetailDto> CreateEventAsync(CreateEventRequest request, CancellationToken ct = default)
        => SendJsonAsync<EventDetailDto>(HttpMethod.Post, "/api/events", request, ct);

    public Task DeleteEventAsync(Guid eventId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"/api/events/{eventId}", ct);

    public async Task<IReadOnlyList<ParticipantDto>> InviteAsync(Guid eventId, IReadOnlyList<Guid> userIds, CancellationToken ct = default)
        => await SendJsonAsync<List<ParticipantDto>>(HttpMethod.Post, $"/api/events/{eventId}/invite", new InviteRequest(userIds), ct);

    public Task ClaimPositionAsync(Guid eventId, string slotId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"/api/events/{eventId}/positions/{Uri.EscapeDataString(slotId)}/claim", ct);

    public Task ReleasePositionAsync(Guid eventId, string slotId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"/api/events/{eventId}/positions/{Uri.EscapeDataString(slotId)}/release", ct);

    public Task<ResultDto> SubmitResultAsync(Guid eventId, SubmitResultRequest request, CancellationToken ct = default)
        => SendJsonAsync<ResultDto>(HttpMethod.Post, $"/api/events/{eventId}/result", request, ct);

    public Task<MyStatsDto> GetMyStatsAsync(CancellationToken ct = default)
        => GetAsync<MyStatsDto>("/api/stats/me", ct);

    public async Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken ct = default)
        => await GetAsync<List<LeagueDto>>("/api/leagues", ct);

    public async Task<IReadOnlyList<LeagueDto>> GetCompletedLeaguesAsync(CancellationToken ct = default)
        => await GetAsync<List<LeagueDto>>("/api/leagues/completed", ct);

    public Task<LeagueDto> CreateLeagueAsync(CreateLeagueRequest request, CancellationToken ct = default)
        => SendJsonAsync<LeagueDto>(HttpMethod.Post, "/api/leagues", request, ct);

    public Task DeleteLeagueAsync(Guid leagueId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"/api/leagues/{leagueId}", ct);

    public Task<LeagueDto> EndLeagueAsync(Guid leagueId, CancellationToken ct = default)
        => SendJsonAsync<LeagueDto>(HttpMethod.Post, $"/api/leagues/{leagueId}/end", null, ct);

    // ---- helpers ----

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return value ?? throw new ApiException((int)response.StatusCode, "Empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = $"Request failed ({(int)response.StatusCode}).";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(JsonOptions, ct);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                detail = problem!.Detail!;
        }
        catch
        {
            // Non-JSON error body; keep the default message.
        }

        throw new ApiException((int)response.StatusCode, detail);
    }

    private record ProblemPayload(int Status, string? Detail, string? Title);
}
