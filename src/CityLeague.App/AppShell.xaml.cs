using CityLeague.App.Views;

namespace CityLeague.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(AppRoutes.EventDetail, typeof(EventDetailPage));
        Routing.RegisterRoute(AppRoutes.SubmitResult, typeof(SubmitResultPage));
        Routing.RegisterRoute(AppRoutes.LocationPicker, typeof(LocationPickerPage));
    }
}
