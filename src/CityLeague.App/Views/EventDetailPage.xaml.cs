using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class EventDetailPage : ContentPage
{
    private readonly EventDetailViewModel _vm;

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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.DisappearingCommand.Execute(null);
    }
}
