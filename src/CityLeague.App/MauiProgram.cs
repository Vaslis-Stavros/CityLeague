using CityLeague.App.Helpers;
using CityLeague.App.Services;
using CityLeague.App.ViewModels;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace CityLeague.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Outfit-Regular.ttf", "OutfitRegular");
                fonts.AddFont("Outfit-SemiBold.ttf", "OutfitSemiBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var services = builder.Services;

        services.AddSingleton<ApiSettings>();
        services.AddSingleton<ITokenStore, TokenStore>();
        services.AddSingleton<ISocialSignInService, SocialSignInService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddTransient<AuthMessageHandler>();

        // Auth client (no auth handler, used for exchange/refresh/me).
        services.AddHttpClient(AuthService.AuthClientName);

        // Typed API client with bearer + refresh handler.
        services.AddHttpClient<ICityLeagueApi, CityLeagueApi>((sp, client) =>
        {
            var settings = sp.GetRequiredService<ApiSettings>();
            client.BaseAddress = new Uri(settings.BaseUrl);
        }).AddHttpMessageHandler<AuthMessageHandler>();

        services.AddTransient<IEventHubService, EventHubService>();
        services.AddHttpClient(OsmFootballFieldService.HttpClientName, OsmFootballFieldService.ConfigureHttpClient);
        services.AddSingleton<IOsmFootballFieldService, OsmFootballFieldService>();

        // View models.
        services.AddTransient<LoginViewModel>();
        services.AddTransient<OnboardingHandleViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<CreateEventViewModel>();
        services.AddTransient<EventDetailViewModel>();
        services.AddTransient<SubmitResultViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<LeaguesViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<LocationPickerViewModel>();

        var app = builder.Build();
        ServiceHelper.Initialize(app.Services);
        return app;
    }
}
