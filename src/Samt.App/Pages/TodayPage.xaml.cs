using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Calculation;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class TodayPage : Page
{
    public TodayPage()
    {
        ViewModel = new TodayViewModel(new PrayerEngine(), App.Localization, App.State);
        InitializeComponent();
        App.Localization.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            ApplyLabels();
            ViewModel.StartTimer();
        };
    }

    public TodayViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        ApplyLabels();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Keep timer running while page is in back stack; dispose only if needed later.
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLabels();

    private void ApplyLabels()
    {
        FlowDirection = App.Localization.FlowDirection;
        TitleText.Text = App.Localization.Get("NavToday");
        NextLabel.Text = App.Localization.Get("NextPrayer");
        TimesHeader.Text = App.Localization.Get("Results");
        QiblaHeader.Text = App.Localization.Get("Qibla");
        RamadanBadgeText.Text = App.Localization.Get("RamadanBadge");
        ViewModel.RefreshLabels();
        Bindings.Update();
    }
}

