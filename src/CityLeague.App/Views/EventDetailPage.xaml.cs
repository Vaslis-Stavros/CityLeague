using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class EventDetailPage : ContentPage
{
    private readonly EventDetailViewModel _vm;
    private bool _didAnimate;

    public EventDetailPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<EventDetailViewModel>();
        BindingContext = _vm;
        Field.SlotTapped += OnSlotTapped;
    }

    private void OnSlotTapped(object? sender, string slotId)
    {
        if (_vm.SlotTappedCommand.CanExecute(slotId))
            _vm.SlotTappedCommand.Execute(slotId);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);

        if (_didAnimate)
            return;

        _didAnimate = true;
        ContentBlock.Opacity = 0;
        ContentBlock.TranslationY = 16;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            ContentBlock.FadeTo(1, 420, Easing.CubicOut),
            ContentBlock.TranslateTo(0, 0, 420, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.DisappearingCommand.Execute(null);
    }
}
