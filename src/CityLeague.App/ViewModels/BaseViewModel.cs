using CommunityToolkit.Mvvm.ComponentModel;
using CityLeague.App.Services;

namespace CityLeague.App.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Called when <see cref="IsBusy"/> changes. Override in derived VMs — do not declare
    /// <c>OnIsBusyChanged</c> there; that partial belongs to this base class.
    /// </summary>
    protected virtual void OnBusyStateChanged(bool isBusy) { }

    partial void OnIsBusyChanged(bool value) => OnBusyStateChanged(value);

    /// <summary>Runs an operation with busy tracking and friendly error capture.</summary>
    protected async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await operation();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Can't reach the server. Check your connection and try again.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
