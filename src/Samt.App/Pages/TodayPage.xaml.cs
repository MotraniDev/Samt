using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class TodayPage : Page
{
    public TodayPage()
    {
        ViewModel = new TodayViewModel(new PrayerEngine(), App.Localization, App.State);
        InitializeComponent();
        App.Localization.LanguageChanged += OnLanguageChanged;
        SizeChanged += OnSizeChanged;
        Loaded += (_, _) =>
        {
            ApplyLabels();
            ApplyCompactLayout(ActualWidth);
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

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyCompactLayout(e.NewSize.Width);

    /// <summary>Scale hero typography / padding when the content column is narrow.</summary>
    private void ApplyCompactLayout(double width)
    {
        var compact = width < 420;
        var tight = width < 360;

        HeroGrid.MinHeight = tight ? 132 : compact ? 148 : 160;
        NextPrayerNameText.FontSize = tight ? 26 : compact ? 28 : 32;
        CountdownTextBlock.FontSize = tight ? 28 : compact ? 32 : 36;
        HeroContent.Padding = tight
            ? new Thickness(12, 16, 12, 16)
            : compact
                ? new Thickness(14, 20, 14, 20)
                : new Thickness(16, 24, 16, 24);

        // Method circle scales with mini-window (compact chrome is bottom-right, not top).
        var circle = tight ? 76 : compact ? 84 : 92;
        MethodCircle.Width = circle;
        MethodCircle.Height = circle;
        MethodCircle.CornerRadius = new CornerRadius(circle / 2.0);
        MethodCircleText.FontSize = tight ? 8.5 : compact ? 9.5 : 10;
        MethodCircleText.LineHeight = tight ? 11 : compact ? 12 : 13;
        MethodChromeStack.Margin = new Thickness(0, 0, 0, 0);

        OrnamentTop.Opacity = tight ? 0.35 : 0.55;
        OrnamentBottom.Opacity = tight ? 0.35 : 0.55;
        // Bottom inset so content clears far-right bottom compact chrome (lang/theme/exit).
        RootScroll.Padding = tight
            ? new Thickness(12, 12, 12, 110)
            : compact
                ? new Thickness(16, 14, 16, 110)
                : new Thickness(20, 16, 20, 110);
    }

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

    private async void PrayerTime_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PrayerEvent prayer })
        {
            return;
        }

        var current = ViewModel.Rows.FirstOrDefault(r => r.Event == prayer)?.DisplayTime ?? "00:00";
        if (!TimeSpan.TryParseExact(
                LatinDigits.EnsureLatin(current).Trim(),
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out var seed)
            && !TimeSpan.TryParse(LatinDigits.EnsureLatin(current), CultureInfo.InvariantCulture, out seed))
        {
            seed = TimeSpan.Zero;
        }

        var seedOnly = TimeOnly.FromTimeSpan(seed);
        var timeBox = new TextBox
        {
            Style = (Style)Application.Current.Resources["LatinDigitsTextBox"],
            Text = LatinDigits.Time(seedOnly),
            PlaceholderText = "05:30",
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Language = "en-US"
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = App.Localization.Get("EditPrayerTime") + " — " + App.Localization.GetPrayerName(prayer),
            PrimaryButtonText = App.Localization.Get("SaveLocation"),
            CloseButtonText = App.Localization.Get("OverlayDismiss"),
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "HH:mm",
                        Opacity = 0.65,
                        FontSize = 12,
                        Language = "en-US"
                    },
                    timeBox
                }
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var text = LatinDigits.EnsureLatin(timeBox.Text ?? string.Empty).Trim();
        if (!TryParseHm(text, out var tod))
        {
            var err = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = App.Localization.Get("EditPrayerTime"),
                Content = App.Localization.Get("InvalidPrayerTime"),
                CloseButtonText = "OK"
            };
            await err.ShowAsync();
            return;
        }

        await ViewModel.SetManualTimeAsync(prayer, tod);
    }

    private async void ResetPrayerTime_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PrayerEvent prayer })
        {
            return;
        }

        await ViewModel.ClearManualTimeAsync(prayer);
    }

    private static bool TryParseHm(string text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (TimeSpan.TryParseExact(text, @"h\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(text, @"h\:mm\:ss", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        // Accept "5:30" without leading zero via general parse.
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        return false;
    }
}
