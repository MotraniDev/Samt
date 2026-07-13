using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Samt_App.Helpers;
using Samt_App.Pages;
using Samt_App.Services;

namespace Samt_App;

public sealed partial class MainWindow : Window
{
    private bool _navReady;
    private bool _suppressChromeEvents = true;

    public MainWindow()
    {
        InitializeComponent();
        // Do NOT set ExtendsContentIntoTitleBar without SetTitleBar — can yield an invisible window.

        App.Localization.LanguageChanged += (_, _) =>
        {
            if (!_suppressChromeEvents)
            {
                ApplyChromeLabels();
            }
        };

        ApplyChromeLabels();
        SyncLanguageBox();
        SyncThemeBox();
        _suppressChromeEvents = false;

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
        // Expanded footer only when the pane is open in Left mode; compact icons when collapsed.
        var expanded = NavView.IsPaneOpen
                       && NavView.DisplayMode is NavigationViewDisplayMode.Expanded
                           or NavigationViewDisplayMode.Compact;

        // LeftCompact with closed pane: show compact chrome over content start edge.
        // When pane is fully open, use the footer stack only.
        var showCompact = !NavView.IsPaneOpen
                          || NavView.DisplayMode == NavigationViewDisplayMode.Minimal;

        PaneFooterExpanded.Visibility = expanded && NavView.IsPaneOpen
            ? Visibility.Visible
            : Visibility.Collapsed;

        // When pane is open but narrow (still shows footer area), keep expanded footer.
        if (NavView.IsPaneOpen && NavView.DisplayMode != NavigationViewDisplayMode.Minimal)
        {
            PaneFooterExpanded.Visibility = Visibility.Visible;
            showCompact = false;
        }

        CompactChrome.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;

        // Dock compact chrome to the pane edge (start = left in LTR, right in RTL).
        if (Content is FrameworkElement root)
        {
            var rtl = root.FlowDirection == FlowDirection.RightToLeft;
            CompactChrome.HorizontalAlignment = rtl
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            CompactChrome.Margin = rtl
                ? new Thickness(0, 0, 4, 10)
                : new Thickness(4, 0, 0, 10);
        }
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
        NavDiagnostics.Content = loc.Get("NavDiagnostics");
        NavDesignLab.Content = loc.Get("NavDesignLab");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavToday, loc.Get("NavToday"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavLocations, loc.Get("NavLocations"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavAlerts, loc.Get("NavAlerts"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavAdhkar, loc.Get("NavAdhkar"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavDiagnostics, loc.Get("NavDiagnostics"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavDesignLab, loc.Get("NavDesignLab"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LanguageBox, loc.Get("Language"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ThemeBox, loc.Get("Theme"));
        ThemeSystemItem.Content = loc.Get("ThemeSystem");
        ThemeLightItem.Content = loc.Get("ThemeLight");
        ThemeDarkItem.Content = loc.Get("ThemeDark");
        ExitAppButtonLabel.Text = loc.Get("ExitApp");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ExitAppButton, loc.Get("ExitApp"));
        ToolTipService.SetToolTip(CompactLangButton, loc.Get("Language"));
        ToolTipService.SetToolTip(CompactThemeButton, loc.Get("Theme"));
        ToolTipService.SetToolTip(CompactExitButton, loc.Get("ExitApp"));
        Title = loc.Get("AppDisplayName") + " — " + loc.Get("AppTagline");

        UpdateChromeForPaneState();

        // Keep tray menu labels in sync with UI language.
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

    private async void CompactLangButton_OnClick(object sender, RoutedEventArgs e)
    {
        var next = App.Localization.CurrentLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "ar";
        App.Localization.SetLanguage(next);
        await App.State.UpdateAsync(s => s.With(language: next.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en-US"));
        SyncLanguageBox();
        ApplyChromeLabels();
    }

    private async void CompactThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var choice = App.Theme.Current switch
        {
            AppThemeChoice.Light => AppThemeChoice.Dark,
            AppThemeChoice.Dark => AppThemeChoice.System,
            _ => AppThemeChoice.Light
        };

        var themeKey = choice switch
        {
            AppThemeChoice.Light => "light",
            AppThemeChoice.Dark => "dark",
            _ => "system"
        };

        App.Theme.Apply(this, choice);
        await App.State.UpdateAsync(s => s.With(theme: themeKey));
        SyncThemeBox();
    }

    private void SyncLanguageBox()
    {
        var lang = App.Localization.CurrentLanguage;
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, lang, StringComparison.OrdinalIgnoreCase)
                || (lang.StartsWith("ar", StringComparison.OrdinalIgnoreCase) && item.Tag as string == "ar"))
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }
    }

    private void SyncThemeBox()
    {
        var tag = App.Theme.Current switch
        {
            AppThemeChoice.Light => "Light",
            AppThemeChoice.Dark => "Dark",
            _ => "System"
        };

        foreach (var item in ThemeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                ThemeBox.SelectedItem = item;
                break;
            }
        }
    }

    private async void LanguageBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChromeEvents)
        {
            return;
        }

        if (LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var language = tag.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en-US";
        if (string.Equals(App.State.Settings.Language, language, StringComparison.OrdinalIgnoreCase)
            && string.Equals(App.Localization.CurrentLanguage, language == "ar" ? "ar" : "en-US", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        App.Localization.SetLanguage(tag);
        await App.State.UpdateAsync(s => s.With(language: language));
        ApplyChromeLabels();
    }

    private async void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChromeEvents)
        {
            return;
        }

        if (ThemeBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var choice = tag switch
        {
            "Light" => AppThemeChoice.Light,
            "Dark" => AppThemeChoice.Dark,
            _ => AppThemeChoice.System
        };

        var themeKey = tag.ToLowerInvariant();
        if (App.Theme.Current == choice
            && string.Equals(App.State.Settings.Theme, themeKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        App.Theme.Apply(this, choice);
        await App.State.UpdateAsync(s => s.With(theme: themeKey));
    }
}
