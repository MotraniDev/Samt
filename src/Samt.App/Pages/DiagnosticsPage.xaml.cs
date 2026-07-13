using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        ViewModel = new DiagnosticsViewModel(new PrayerEngine(), App.Localization, App.State);
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLanguageUi();
        Loaded += (_, _) => ApplyLanguageUi();
    }

    public DiagnosticsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyLanguageUi();
        Bindings.Update();
    }

    private void ApplyLanguageUi()
    {
        FlowDirection = App.Localization.FlowDirection;
        TitleText.Text = App.Localization.Get("NavDiagnostics");
        InputsHeader.Text = App.Localization.Get("Inputs");
        LocationLabel.Text = App.Localization.Get("Location");
        MethodLabel.Text = App.Localization.Get("Method");
        DateLabel.Text = App.Localization.Get("Date");
        RecalculateButton.Content = App.Localization.Get("Recalculate");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            RecalculateButton, App.Localization.Get("Recalculate"));
        LatLabel.Text = App.Localization.Get("Latitude");
        LonLabel.Text = App.Localization.Get("Longitude");
        TzLabel.Text = App.Localization.Get("TimeZone");
        ResultsHeader.Text = App.Localization.Get("Results");
        AsrLabel.Text = App.Localization.Get("AsrMadhab");
        AsrStandardRadio.Content = App.Localization.Get("AsrStandard");
        AsrHanafiRadio.Content = App.Localization.Get("AsrHanafi");
        HijriOffsetLabel.Text = App.Localization.Get("HijriDayOffset");
        HijriOffsetHint.Text = App.Localization.Get("HijriDayOffsetHint");
        AppOptionsHeader.Text = App.Localization.Get("AppOptions");
        AutoStartToggle.Header = App.Localization.Get("AutoStartEnabled");
        AutoStartToggle.OffContent = App.Localization.Get("ToggleOff");
        AutoStartToggle.OnContent = App.Localization.Get("ToggleOn");
        MissedResumeToggle.Header = App.Localization.Get("ShowMissedAlertOnResume");
        MissedResumeToggle.OffContent = App.Localization.Get("ToggleOff");
        MissedResumeToggle.OnContent = App.Localization.Get("ToggleOn");
        RefreshStatusButton.Content = App.Localization.Get("RefreshProcessStatus");
        SyncOptionToggles();
        ViewModel.RefreshLabels();
        Bindings.Update();
    }

    private void SyncOptionToggles()
    {
        AutoStartToggle.IsOn = ViewModel.AutoStartEnabled;
        MissedResumeToggle.IsOn = ViewModel.ShowMissedAlertOnResume;
    }

    private void AutoStartToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ViewModel.AutoStartEnabled = AutoStartToggle.IsOn;
    }

    private void MissedResumeToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ViewModel.ShowMissedAlertOnResume = MissedResumeToggle.IsOn;
    }

    private void RefreshStatusButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.RefreshProcessStatus();

    private void RecalculateButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel.Recalculate();

    private void AsrStandardRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            ViewModel.SelectedAsrMadhab = AsrMadhab.Standard;
        }
    }

    private void AsrHanafiRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            ViewModel.SelectedAsrMadhab = AsrMadhab.Hanafi;
        }
    }

    private void HijriOffsetBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!IsLoaded || double.IsNaN(args.NewValue))
        {
            return;
        }

        ViewModel.HijriDayOffset = (int)Math.Round(args.NewValue);
    }
}
