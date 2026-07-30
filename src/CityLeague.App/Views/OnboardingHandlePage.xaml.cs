using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class OnboardingHandlePage : ContentPage
{
    public OnboardingHandlePage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<OnboardingHandleViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, StatusBarTheme.BrandTop);
    }
}
