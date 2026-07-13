using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Samt_App.Pages;
using Samt_App.Services;

namespace Samt_App;

public sealed partial class MainWindow : Window
{
    private bool _navReady;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        App.Localization.LanguageChanged += (_, _) => ApplyChromeLabels();
        ApplyChromeLabels();
        SyncLanguageBox();
        SyncThemeBox();
    }

    private void NavView_OnLoaded(object sender, RoutedEventArgs e)
    {
        _navReady = true;
        ContentFrame.Navigate(typeof(TodayPage));
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
            _ => typeof(TodayPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
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
                || (lang.StartsWith("ar") && item.Tag as string == "ar"))
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
        if (LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        App.Localization.SetLanguage(tag);
        await App.State.UpdateAsync(s => s.With(language: tag.StartsWith("ar") ? "ar" : "en-US"));
        ApplyChromeLabels();
    }

    private async void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
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

        App.Theme.Apply(this, choice);
        await App.State.UpdateAsync(s => s.With(theme: tag.ToLowerInvariant()));
    }
}
