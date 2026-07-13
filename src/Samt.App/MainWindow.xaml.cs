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

    private void NavView_OnLoaded(object sender, RoutedEventArgs e)
    {
        _navReady = true;
        if (ContentFrame.Content is null)
        {
            ContentFrame.Navigate(typeof(TodayPage));
            LaunchLog.Write("MainWindow navigated to TodayPage on Loaded");
        }

        ApplyChromeLabels();
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
        NavDiagnostics.Content = loc.Get("NavDiagnostics");
        NavDesignLab.Content = loc.Get("NavDesignLab");
        ThemeSystemItem.Content = loc.Get("ThemeSystem");
        ThemeLightItem.Content = loc.Get("ThemeLight");
        ThemeDarkItem.Content = loc.Get("ThemeDark");
        Title = loc.Get("AppDisplayName") + " — " + loc.Get("AppTagline");
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
