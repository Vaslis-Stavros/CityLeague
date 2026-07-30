namespace CityLeague.App;

/// <summary>Central place for Shell route strings.</summary>
public static class AppRoutes
{
    public const string Login = "//login";
    public const string Onboarding = "//onboarding";
    public const string Home = "//home";
    public const string Contacts = "//contacts";
    public const string Create = "//create";
    public const string Leagues = "//leagues";
    public const string More = "//more";

    // Detail / secondary routes (registered with Routing.RegisterRoute).
    public const string EventDetail = "eventdetail";
    public const string SubmitResult = "submitresult";
    public const string LocationPicker = "locationpicker";
    public const string History = "historypage";
    public const string Profile = "profilepage";
    public const string LeagueDetail = "leaguedetail";
    public const string Settings = "settingspage";
}
