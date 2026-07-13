using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Samt_App.Helpers;
using Samt_App.Pages;
using Samt_App.Services;

namespace Samt_App;

public sealed partial class MainWindow : Window
{
    private bool _navReady;

    public MainWindow()
    {
        InitializeComponent();

        CustomWindowChrome.StyleCaptionButton(TitleMinButton);
        CustomWindowChrome.StyleCaptionButton(TitleMaxButton);
        CustomWindowChrome.StyleCaptionButton(TitleCloseButton, isClose: true);
        CustomWindowChrome.Apply(
            this,
            TitleBarDrag,
            TitleMinButton,
            TitleMaxButton,
            TitleCloseButton,
            allowMaximize: true,
            // Never Window.Close() here — that kills the desktop window permanently.
            onCloseClick: App.RequestHideToTray);

        App.Localization.LanguageChanged += (_, _) => ApplyChromeLabels();
        if (App.State is not null)
        {
            App.State.SettingsChanged += (_, _) => ApplyShellOpacityFromSettings();
        }

        ApplyChromeLabels();
        ApplyShellOpacityFromSettings();

        // Navigate early so content exists even if Loaded is delayed.
        try
        {
            ContentFrame.Navigate(typeof(TodayPage));
            LaunchLog.Write("MainWindow navigated to TodayPage in ctor");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"MainWindow ctor navigate failed: {ex}");
        }
    }

    public void ApplyShellOpacityFromSettings()
    {
        try
        {
            var opacity = App.State?.Settings.WindowOpacity ?? 1.0;
            WindowChromeOpacity.Apply(this, opacity);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"MainWindow opacity: {ex.Message}");
        }
    }

    /// <summary>Collapse the navigation pane (compact rail) — used on launch / tray show.</summary>
    public void CollapseNavigationPane()
    {
        try
        {
            NavView.IsPaneOpen = false;
            UpdateChromeForPaneState();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"CollapseNavigationPane: {ex.Message}");
        }
    }

    private void NavView_OnLoaded(object sender, RoutedEventArgs e)
    {
        _navReady = true;
        if (ContentFrame.Content is null)
        {
            ContentFrame.Navigate(typeof(TodayPage));
            LaunchLog.Write("MainWindow navigated to TodayPage on Loaded");
        }

        ApplyChromeLabels();
        UpdateChromeForPaneState();
    }

    private void NavView_OnPaneOpened(NavigationView sender, object args)
        => UpdateChromeForPaneState();

    private void NavView_OnPaneClosed(NavigationView sender, object args)
        => UpdateChromeForPaneState();

    private void NavView_OnDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        => UpdateChromeForPaneState();

    private void UpdateChromeForPaneState()
    {
        var expanded = NavView.IsPaneOpen
                       && NavView.DisplayMode is NavigationViewDisplayMode.Expanded
                           or NavigationViewDisplayMode.Compact;

        var showCompact = !NavView.IsPaneOpen
                          || NavView.DisplayMode == NavigationViewDisplayMode.Minimal;

        PaneFooterExpanded.Visibility = expanded && NavView.IsPaneOpen
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (NavView.IsPaneOpen && NavView.DisplayMode != NavigationViewDisplayMode.Minimal)
        {
            PaneFooterExpanded.Visibility = Visibility.Visible;
            showCompact = false;
        }

        CompactChrome.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;
        CompactChrome.HorizontalAlignment = HorizontalAlignment.Left;
        CompactChrome.VerticalAlignment = VerticalAlignment.Bottom;
        CompactChrome.Margin = new Thickness(8, 0, 0, 10);
    }

    private void NavView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_navReady || args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        var pageType = tag switch
        {
            "locations" => typeof(LocationsPage),
            "alerts" => typeof(AlertsPage),
            "adhkar" => typeof(AdhkarPage),
            "settings" => typeof(SettingsPage),
            "diagnostics" => typeof(DiagnosticsPage),
            "designlab" => typeof(DesignLabPage),
            _ => typeof(TodayPage)
        };

        if (ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        try
        {
            ContentFrame.Navigate(pageType);
            LaunchLog.Write($"Navigated to {pageType.Name}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Navigate to {pageType.Name} failed: {ex}");
        }
    }

    private void ApplyChromeLabels()
    {
        var loc = App.Localization;
        if (Content is FrameworkElement root)
        {
            root.FlowDirection = loc.FlowDirection;
        }

        NavToday.Content = loc.Get("NavToday");
        NavLocations.Content = loc.Get("NavLocations");
        NavAlerts.Content = loc.Get("NavAlerts");
        NavAdhkar.Content = loc.Get("NavAdhkar");
        NavSettings.Content = loc.Get("NavSettings");
        NavDiagnostics.Content = loc.Get("NavDiagnostics");
        NavDesignLab.Content = loc.Get("NavDesignLab");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavToday, loc.Get("NavToday"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavLocations, loc.Get("NavLocations"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavAlerts, loc.Get("NavAlerts"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavAdhkar, loc.Get("NavAdhkar"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavSettings, loc.Get("NavSettings"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavDiagnostics, loc.Get("NavDiagnostics"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavDesignLab, loc.Get("NavDesignLab"));
        ExitAppButtonLabel.Text = loc.Get("ExitApp");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ExitAppButton, loc.Get("ExitApp"));
        ToolTipService.SetToolTip(CompactExitButton, loc.Get("ExitApp"));
        Title = loc.Get("AppDisplayName") + " — " + loc.Get("AppTagline");

        UpdateChromeForPaneState();

        if (Application.Current is App)
        {
            try
            {
                App.RefreshTrayMenuLabels();
            }
            catch
            {
                // non-fatal
            }
        }
    }

    private void ExitAppButton_OnClick(object sender, RoutedEventArgs e)
    {
        App.RequestExit();
    }
}
