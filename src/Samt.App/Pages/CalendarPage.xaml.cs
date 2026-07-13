using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Samt.Core.Formatting;
using Samt_App.ViewModels;
using Windows.UI;

namespace Samt_App.Pages;

public sealed partial class CalendarPage : Page
{
    public CalendarViewModel ViewModel { get; }

    public CalendarPage()
    {
        ViewModel = new CalendarViewModel(App.State, App.Localization);
        InitializeComponent();
        DataContext = ViewModel;
        App.Localization.LanguageChanged += (_, _) => ApplyLanguageUi();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalendarViewModel.MonthTitle)
                or nameof(CalendarViewModel.Subtitle)
                or nameof(CalendarViewModel.Days)
                or null)
            {
                SyncHeader();
                RebuildGrid();
            }
        };
        Loaded += (_, _) =>
        {
            ApplyLanguageUi();
            SyncHeader();
            RebuildGrid();
        };
    }

    private void ApplyLanguageUi()
    {
        var loc = App.Localization;
        FlowDirection = loc.FlowDirection;
        TitleText.Text = loc.Get("NavCalendar");
        DisclaimerText.Text = loc.Get("CalendarDisclaimer");
        TodayButton.Content = loc.Get("CalendarToday");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PrevButton, loc.Get("CalendarPrevMonth"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NextButton, loc.Get("CalendarNextMonth"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TodayButton, loc.Get("CalendarToday"));
        ViewModel.Refresh();
        SyncHeader();
        RebuildGrid();
    }

    private void SyncHeader()
    {
        MonthTitleText.Text = ViewModel.MonthTitle;
        SubtitleText.Text = ViewModel.Subtitle;
    }

    private void RebuildGrid()
    {
        WeekdayHeaderGrid.Children.Clear();
        WeekdayHeaderGrid.ColumnDefinitions.Clear();
        for (var c = 0; c < 7; c++)
        {
            WeekdayHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var header = new TextBlock
            {
                Text = c < ViewModel.WeekdayHeaders.Count ? ViewModel.WeekdayHeaders[c] : "",
                FontSize = 11,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetColumn(header, c);
            WeekdayHeaderGrid.Children.Add(header);
        }

        DaysGrid.Children.Clear();
        DaysGrid.ColumnDefinitions.Clear();
        DaysGrid.RowDefinitions.Clear();
        for (var c = 0; c < 7; c++)
        {
            DaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var days = ViewModel.Days;
        var rows = Math.Max(1, (int)Math.Ceiling(days.Count / 7.0));
        for (var r = 0; r < rows; r++)
        {
            DaysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var col = i % 7;
            var row = i / 7;
            var cell = BuildDayCell(day);
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            DaysGrid.Children.Add(cell);
        }
    }

    private FrameworkElement BuildDayCell(CalendarDayVm day)
    {
        if (day.IsPlaceholder)
        {
            return new Border { MinHeight = 84, Opacity = 0.15 };
        }

        var border = new Border
        {
            MinHeight = 84,
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = day.IsToday
                ? (Brush)Application.Current.Resources["SamtGoldBrush"]
                : (Brush)Application.Current.Resources["SamtHairlineBrush"],
            BorderThickness = new Thickness(day.IsToday ? 2 : 1),
            Tag = day
        };

        if (day.IsRamadan)
        {
            border.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xC4, 0xA3, 0x5A));
        }

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = day.HijriDayText,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["SamtGoldBrush"],
            FontFamily = (FontFamily)Application.Current.Resources["SamtDisplayFont"],
            Style = (Style)Application.Current.Resources["LatinDigitsTextBlock"]
        });
        stack.Children.Add(new TextBlock
        {
            Text = day.GregorianText,
            FontSize = 11,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["LatinDigitsTextBlock"]
        });

        if (!string.IsNullOrEmpty(day.SpecialLabel))
        {
            stack.Children.Add(new TextBlock
            {
                Text = day.SpecialLabel,
                FontSize = 10,
                Opacity = 0.9,
                Foreground = (Brush)Application.Current.Resources["SamtMintBrush"],
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        if (day.ShowIslamicDot || day.ShowCountryDot)
        {
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            if (day.ShowIslamicDot)
            {
                dots.Children.Add(new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)Application.Current.Resources["SamtGoldBrush"]
                });
            }

            if (day.ShowCountryDot)
            {
                dots.Children.Add(new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)Application.Current.Resources["SamtMintBrush"]
                });
            }

            stack.Children.Add(dots);
        }

        border.Child = stack;
        border.Tapped += async (_, _) => await OpenDaySheetAsync(day);
        return border;
    }

    private async Task OpenDaySheetAsync(CalendarDayVm day)
    {
        var loc = App.Localization;
        var hijriName = loc.Get($"Hijri.Month.{day.Hijri.Month}");
        var hijriLine = $"{LatinDigits.Number(day.Hijri.Day)} {hijriName} {LatinDigits.Number(day.Hijri.Year)}";
        var gregLine = LatinDigits.Date(day.CivilDate, "dddd, d MMMM yyyy");

        var body = new StackPanel { Spacing = 10, MinWidth = 280 };
        body.Children.Add(new TextBlock
        {
            Text = hijriLine,
            FontFamily = (FontFamily)Application.Current.Resources["SamtDisplayFont"],
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["SamtGoldBrush"],
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new TextBlock
        {
            Text = gregLine,
            Style = (Style)Application.Current.Resources["LatinDigitsTextBlock"],
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        });

        ToggleSwitch? muteToggle = null;
        if (day.SpecialDay is { } special)
        {
            foreach (var key in special.DisplayKeys)
            {
                body.Children.Add(new TextBlock
                {
                    Text = loc.Get(key),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (special.Definitions.Any(d => d.IsCommonlyObserved))
            {
                body.Children.Add(new TextBlock
                {
                    Text = loc.Get("SpecialDay.CommonlyObservedHint"),
                    FontSize = 12,
                    Opacity = 0.65,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var anyMuted = special.DefinitionIds.All(ViewModel.IsDefinitionMuted);
            muteToggle = new ToggleSwitch
            {
                Header = loc.Get("CalendarMuteReminder"),
                IsOn = anyMuted,
                OnContent = loc.Get("ToggleOn"),
                OffContent = loc.Get("ToggleOff")
            };
            body.Children.Add(muteToggle);
            body.Children.Add(new TextBlock
            {
                Text = loc.Get("CalendarMuteHint"),
                FontSize = 12,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            body.Children.Add(new TextBlock
            {
                Text = loc.Get("CalendarOrdinaryDay"),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new ContentDialog
        {
            Title = loc.Get("CalendarDaySheetTitle"),
            Content = body,
            CloseButtonText = loc.Get("CalendarClose"),
            XamlRoot = XamlRoot,
            FlowDirection = loc.FlowDirection
        };

        await dialog.ShowAsync();

        if (day.SpecialDay is { } specialDay && muteToggle is not null)
        {
            var shouldMute = muteToggle.IsOn;
            var currentlyMuted = specialDay.DefinitionIds.All(ViewModel.IsDefinitionMuted);
            if (shouldMute != currentlyMuted)
            {
                await ViewModel.SetDayMutedAsync(specialDay, shouldMute);
                RebuildGrid();
            }
        }
    }

    private void PrevButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PrevMonth();
        SyncHeader();
        RebuildGrid();
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.NextMonth();
        SyncHeader();
        RebuildGrid();
    }

    private void TodayButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.JumpToToday();
        SyncHeader();
        RebuildGrid();
    }
}
