using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Storage;
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
                or nameof(CalendarViewModel.ModeToggleLabel)
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
        ModeToggleButton.Content = ViewModel.ModeToggleLabel;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PrevButton, loc.Get("CalendarPrevMonth"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NextButton, loc.Get("CalendarNextMonth"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TodayButton, loc.Get("CalendarToday"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ModeToggleButton, loc.Get("CalendarToggleMode"));
        ViewModel.Refresh();
        SyncHeader();
        RebuildGrid();
    }

    private void SyncHeader()
    {
        MonthTitleText.Text = ViewModel.MonthTitle;
        SubtitleText.Text = ViewModel.Subtitle;
        ModeToggleButton.Content = ViewModel.ModeToggleLabel;
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
            var cell = BuildDayCell(day);
            Grid.SetColumn(cell, i % 7);
            Grid.SetRow(cell, i / 7);
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
            Text = day.PrimaryDayText,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["SamtGoldBrush"],
            FontFamily = (FontFamily)Application.Current.Resources["SamtDisplayFont"],
            Style = (Style)Application.Current.Resources["LatinDigitsTextBlock"]
        });
        stack.Children.Add(new TextBlock
        {
            Text = day.SecondaryText,
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

        if (day.ShowIslamicDot || day.ShowCountryDot || day.ShowUserDot)
        {
            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 4, 0, 0)
            };
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

            if (day.ShowUserDot)
            {
                dots.Children.Add(new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x7E, 0xB6, 0xFF))
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

        var body = new StackPanel { Spacing = 10, MinWidth = 300 };
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

        // Add / edit form controls (declared before list so rows can load into them)
        var titleBox = new TextBox { PlaceholderText = loc.Get("CalendarReminderTitle") };
        var noteBox = new TextBox { PlaceholderText = loc.Get("CalendarReminderNote") };
        var timeBox = new TextBox
        {
            Text = "09:00",
            PlaceholderText = "09:00",
            Style = (Style)Application.Current.Resources["LatinDigitsTextBox"]
        };
        var repeatBox = new TextBox
        {
            Text = "1",
            PlaceholderText = loc.Get("CalendarReminderRepeatCount"),
            Style = (Style)Application.Current.Resources["LatinDigitsTextBox"]
        };
        var intervalBox = new TextBox
        {
            Text = "5",
            PlaceholderText = loc.Get("CalendarReminderInterval"),
            Style = (Style)Application.Current.Resources["LatinDigitsTextBox"]
        };
        Guid? editingId = null;

        // Existing user reminders
        var existing = ViewModel.RemindersForDay(day.CivilDate);
        if (existing.Count > 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = loc.Get("CalendarUserReminders"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            });
            foreach (var rem in existing)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new TextBlock
                {
                    Text = $"{LatinDigits.EnsureLatin(rem.Time)} · {rem.Title}"
                           + (string.IsNullOrWhiteSpace(rem.Note) ? "" : $" — {rem.Note}")
                           + (rem.RepeatCount > 1
                               ? $" ({LatinDigits.Number(rem.RepeatCount)}× / {LatinDigits.Number(rem.IntervalMinutes)} min)"
                               : ""),
                    TextWrapping = TextWrapping.Wrap,
                    Style = (Style)Application.Current.Resources["LatinDigitsTextBlock"]
                };
                var captured = rem;
                text.PointerPressed += (_, _) =>
                {
                    editingId = captured.Id;
                    titleBox.Text = captured.Title;
                    noteBox.Text = captured.Note ?? "";
                    timeBox.Text = captured.Time;
                    repeatBox.Text = LatinDigits.Number(captured.RepeatCount);
                    intervalBox.Text = LatinDigits.Number(captured.IntervalMinutes);
                };
                Grid.SetColumn(text, 0);
                row.Children.Add(text);
                var del = new Button { Content = "×", Tag = rem.Id, MinWidth = 36 };
                var id = rem.Id;
                del.Click += async (_, _) =>
                {
                    await ViewModel.DeleteUserReminderAsync(id);
                };
                Grid.SetColumn(del, 1);
                row.Children.Add(del);
                body.Children.Add(row);
            }
        }

        body.Children.Add(new TextBlock
        {
            Text = loc.Get("CalendarAddReminder"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 0)
        });
        body.Children.Add(titleBox);
        body.Children.Add(noteBox);
        body.Children.Add(Labeled(loc.Get("SpecialDayReminderTime"), timeBox));
        body.Children.Add(Labeled(loc.Get("CalendarReminderRepeatCount"), repeatBox));
        body.Children.Add(Labeled(loc.Get("CalendarReminderInterval"), intervalBox));

        var dialog = new ContentDialog
        {
            Title = loc.Get("CalendarDaySheetTitle"),
            Content = new ScrollViewer
            {
                Content = body,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = loc.Get("CalendarSaveReminder"),
            CloseButtonText = loc.Get("CalendarClose"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            FlowDirection = loc.FlowDirection
        };

        var result = await dialog.ShowAsync();

        if (day.SpecialDay is { } specialDay && muteToggle is not null)
        {
            var shouldMute = muteToggle.IsOn;
            var currentlyMuted = specialDay.DefinitionIds.All(ViewModel.IsDefinitionMuted);
            if (shouldMute != currentlyMuted)
            {
                await ViewModel.SetDayMutedAsync(specialDay, shouldMute);
            }
        }

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(titleBox.Text))
        {
            var time = SettingsJson.NormalizeClockTime(timeBox.Text, "09:00");
            var repeats = ParseInt(repeatBox.Text, 1, 1, 20);
            var interval = ParseInt(intervalBox.Text, 5, 0, 1440);
            if (editingId is { } editId)
            {
                await ViewModel.UpdateUserReminderAsync(
                    editId,
                    titleBox.Text,
                    noteBox.Text ?? "",
                    time,
                    repeats,
                    interval);
            }
            else
            {
                await ViewModel.AddUserReminderAsync(
                    day.CivilDate,
                    titleBox.Text,
                    noteBox.Text ?? "",
                    time,
                    repeats,
                    interval);
            }
        }

        RebuildGrid();
    }

    private static StackPanel Labeled(string label, FrameworkElement control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Opacity = 0.75, FontSize = 12 });
        panel.Children.Add(control);
        return panel;
    }

    private static int ParseInt(string? text, int fallback, int min, int max)
    {
        var raw = LatinDigits.EnsureLatin(text ?? "").Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            n = fallback;
        }

        return Math.Clamp(n, min, max);
    }

    private async void ModeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleModeAsync();
        SyncHeader();
        RebuildGrid();
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
