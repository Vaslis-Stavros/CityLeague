namespace CityLeague.App.Helpers;

/// <summary>Provides access to the DI container for pages created by Shell templates.</summary>
public static class ServiceHelper
{
    public static IServiceProvider Services { get; private set; } = default!;

    public static void Initialize(IServiceProvider services) => Services = services;

    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}
