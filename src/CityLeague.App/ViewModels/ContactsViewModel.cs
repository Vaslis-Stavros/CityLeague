using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public partial class ContactsViewModel(ICityLeagueApi api) : BaseViewModel
{
    public ObservableCollection<ContactDto> Contacts { get; } = [];
    public ObservableCollection<UserSearchResultDto> SearchResults { get; } = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var contacts = await api.GetContactsAsync();
            Contacts.Clear();
            foreach (var c in contacts)
                Contacts.Add(c);
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
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Trim().Length < 2)
        {
            SearchResults.Clear();
            return;
        }

        await RunAsync(async () =>
        {
            var results = await api.SearchUsersAsync(SearchQuery.Trim());
            SearchResults.Clear();
            foreach (var r in results)
                SearchResults.Add(r);
        });
    }

    [RelayCommand]
    private async Task AddAsync(UserSearchResultDto user)
    {
        if (user is null) return;
        await RunAsync(async () =>
        {
            await api.AddContactAsync(new CreateContactRequest(user.Id, null));
            SearchResults.Remove(user);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task AcceptAsync(ContactDto contact)
    {
        if (contact is null) return;
        await RunAsync(async () =>
        {
            await api.AcceptContactAsync(contact.User.Id);
            await LoadAsync();
        });
    }
}
