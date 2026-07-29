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
    [NotifyPropertyChangedFor(nameof(HasSearchResults))]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool ShowEmptyContacts => Contacts.Count == 0 && !HasSearchResults;

    public string ContactsSubtitle => Contacts.Count switch
    {
        0 => "Find people by @handle",
        1 => "1 person",
        _ => $"{Contacts.Count} people",
    };

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
            OnPropertyChanged(nameof(ContactsSubtitle));
            OnPropertyChanged(nameof(ShowEmptyContacts));
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
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(ShowEmptyContacts));
            return;
        }

        await RunAsync(async () =>
        {
            var results = await api.SearchUsersAsync(SearchQuery.Trim());
            SearchResults.Clear();
            foreach (var r in results)
                SearchResults.Add(r);
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(ShowEmptyContacts));
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
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(ShowEmptyContacts));
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
