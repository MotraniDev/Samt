using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class LocationsPage : Page
{
    public LocationsPage()
    {
        ViewModel = new LocationsViewModel(App.State, App.Localization);
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLabels();
        Loaded += (_, _) => ApplyLabels();
    }

    public LocationsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyLabels();
        Bindings.Update();
    }

    private void ApplyLabels()
    {
        FlowDirection = App.Localization.FlowDirection;
        ListHeader.Text = App.Localization.Get("SavedLocations");
        EditorHeader.Text = App.Localization.Get("LocationEditor");
        NameLabel.Text = App.Localization.Get("LocationName");
        LatLabel.Text = App.Localization.Get("Latitude");
        LonLabel.Text = App.Localization.Get("Longitude");
        CountryLabel.Text = App.Localization.Get("LocationCountryCode");
        TzLabel.Text = App.Localization.Get("TimeZone");
        ActivateButton.Content = App.Localization.Get("UseLocation");
        NewButton.Content = App.Localization.Get("NewLocation");
        DeleteButton.Content = App.Localization.Get("DeleteLocation");
        SaveButton.Content = App.Localization.Get("SaveLocation");
        GpsButton.Content = App.Localization.Get("UseGps");
        PrivacyNote.Text = App.Localization.Get("LocationPrivacyNote");
        FridaySectionLabel.Text = App.Localization.Get("FridaySection");
        FridayModeLabel.Text = App.Localization.Get("FridayTimeMode");
        FridayFollowDhuhrItem.Content = App.Localization.Get("FridayFollowDhuhr");
        FridayFixedTimeItem.Content = App.Localization.Get("FridayFixedTime");
        FixedFridayLabel.Text = App.Localization.Get("FixedFridayTime");
        SuppressDhuhrCheck.Content = App.Localization.Get("SuppressDhuhrOnFriday");
        PlaceSearchLabel.Text = App.Localization.Get("PlaceSearch");
        PlaceSearchButton.Content = App.Localization.Get("PlaceSearch");
        PlaceSearchAttribution.Text = App.Localization.Get("PlaceSearchAttribution");
        PlaceSearchBox.PlaceholderText = App.Localization.Get("PlaceSearchHint");
    }

    private async void ActivateButton_OnClick(object sender, RoutedEventArgs e)
        => await ViewModel.UseAsActiveAsync();

    private void NewButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.NewManual();

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
        => await ViewModel.DeleteSelectedAsync();

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveManualAsync();
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"Save button failed: {ex}");
        }
    }

    private async void GpsButton_OnClick(object sender, RoutedEventArgs e)
        => await ViewModel.DetectGpsAsync();

    private async void PlaceSearchButton_OnClick(object sender, RoutedEventArgs e)
        => await ViewModel.SearchPlacesAsync();

    private void PlaceResultsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedPlaceResult is not null)
        {
            ViewModel.ApplyPlaceResult(ViewModel.SelectedPlaceResult);
        }
    }

    private void PlaceResultsList_OnDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedPlaceResult is not null)
        {
            ViewModel.ApplyPlaceResult(ViewModel.SelectedPlaceResult);
        }
    }
}
