using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Samt.Core.Adhkar;
using Samt.Core.Formatting;
using Samt_App.Helpers;
using Windows.Graphics;
using WinRT.Interop;

namespace Samt_App.Overlay;

public sealed partial class AdhkarReaderWindow : Window
{
    private readonly AdhkarReadingSession _session = new();
    private int _index;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private CancellationTokenSource? _autoAdvanceCts;

    public AdhkarReaderWindow()
    {
        InitializeComponent();
        CustomWindowChrome.StyleCaptionButton(MinButton);
        CustomWindowChrome.StyleCaptionButton(MaxButton);
        CustomWindowChrome.StyleCaptionButton(CloseCaptionButton, isClose: true);

        // Caption buttons wired in code-behind (avoid double-subscribe from Apply).
        _appWindow = CustomWindowChrome.Apply(
            this,
            TitleBarDrag,
            minimizeButton: null,
            maximizeButton: null,
            closeButton: null,
            new SizeInt32(520, 720),
            allowMaximize: true);

        if (_appWindow is not null)
        {
            _appWindow.Title = "Adhkar";
        }

        ApplyShellOpacity(App.State?.Settings.WindowOpacity ?? 1.0);
        Closed += (_, _) => CancelAutoAdvance();
    }

    public void ApplyShellOpacity(double opacity)
        => WindowChromeOpacity.Apply(this, opacity);

    public void ShowCollection(AdhkarCollectionKind kind, int startIndex = 0)
    {
        CancelAutoAdvance();
        var collection = AdhkarCatalog.Get(kind);
        _session.Bind(collection, reset: true);
        _index = Math.Clamp(startIndex, 0, Math.Max(0, collection.Items.Count - 1));
        ApplyLanguageChrome();
        RebuildPathDots();
        ShowItem();
        ApplyShellOpacity(App.State?.Settings.WindowOpacity ?? 1.0);

        try
        {
            if (_appWindow is null)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            }

            if (_appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
                && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }

            _appWindow.Show();
        }
        catch
        {
            // non-fatal
        }

        Activate();
    }

    private void ApplyLanguageChrome()
    {
        var loc = App.Localization;
        if (Content is FrameworkElement root)
        {
            root.FlowDirection = loc.FlowDirection;
        }

        var collection = _session.Collection;
        TitleText.Text = collection is null ? loc.Get("NavAdhkar") : loc.Get(collection.TitleKey);
        SourceBadge.Text = loc.Get("Adhkar.Source.AzkarMe");
        PrevLabel.Text = loc.Get("AdhkarPrev");
        NextLabel.Text = loc.Get("AdhkarNext");
        MarkDoneLabel.Text = loc.Get("AdhkarMarkDone");
        ResetProgressButton.Content = loc.Get("AdhkarResetProgress");
        TapHint.Text = loc.Get("AdhkarTapToCount");
        CountLabel.Text = loc.Get("AdhkarCount");

        if (loc.IsRightToLeft)
        {
            PrevIcon.Glyph = "\uE76C";
            NextIcon.Glyph = "\uE76B";
        }
        else
        {
            PrevIcon.Glyph = "\uE76B";
            NextIcon.Glyph = "\uE76C";
        }
    }

    private void RebuildPathDots()
    {
        PathDots.Items.Clear();
        var collection = _session.Collection;
        if (collection is null)
        {
            return;
        }

        for (var i = 0; i < collection.Items.Count; i++)
        {
            var idx = i;
            var item = collection.Items[i];
            var done = _session.IsItemComplete(item);
            var current = i == _index;
            var btn = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(11),
                Tag = idx,
                Background = current
                    ? (Brush)Application.Current.Resources["SamtGoldBrush"]
                    : done
                        ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x66, 0xC9, 0xA2, 0x27))
                        : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0),
                Content = new TextBlock
                {
                    Text = done ? "✓" : LatinDigits.Number(i + 1),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = current
                        ? new SolidColorBrush(Microsoft.UI.Colors.Black)
                        : (Brush)Application.Current.Resources["SamtIvoryBrush"]
                }
            };
            btn.Click += PathDot_OnClick;
            PathDots.Items.Add(btn);
        }
    }

    private void PathDot_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int idx })
        {
            CancelAutoAdvance();
            _index = idx;
            ShowItem();
            RebuildPathDots();
        }
    }

    private void ShowItem()
    {
        var collection = _session.Collection;
        if (collection is null || collection.Items.Count == 0)
        {
            ArabicText.Text = string.Empty;
            TranslationText.Text = string.Empty;
            BenefitText.Text = string.Empty;
            ReferenceText.Text = string.Empty;
            IstiadhahText.Visibility = Visibility.Collapsed;
            BasmalaText.Visibility = Visibility.Collapsed;
            CountDisplay.Text = string.Empty;
            SetProgressLabel.Text = string.Empty;
            SetProgressBar.Value = 0;
            CompleteCheck.Visibility = Visibility.Collapsed;
            return;
        }

        var item = collection.Items[_index];
        IstiadhahText.Visibility = AdhkarBasmala.ShowsIstiadhah(item) ? Visibility.Visible : Visibility.Collapsed;
        BasmalaText.Visibility = AdhkarBasmala.ShowsBasmala(item) ? Visibility.Visible : Visibility.Collapsed;
        ArabicText.Text = AdhkarBasmala.BodyArabic(item);
        ReferenceText.Text = item.Reference ?? string.Empty;
        BenefitText.Text = item.BenefitText ?? string.Empty;
        TranslationText.Text = item.TranslationKey is null
            ? string.Empty
            : App.Localization.Get(item.TranslationKey);

        var count = _session.GetCount(item.Id);
        var target = _session.GetTarget(item);
        CountDisplay.Text = LatinDigits.EnsureLatin($"{count} / {target}");
        var done = _session.IsItemComplete(item);
        CompleteCheck.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
        CountButton.IsEnabled = !done;
        MarkDoneButton.IsEnabled = !done;

        SetProgressBar.Value = _session.ItemProgress;
        SetProgressLabel.Text = LatinDigits.EnsureLatin(
            string.Format(
                App.Localization.Get("AdhkarSetProgressFormat"),
                _session.CompletedItemCount,
                collection.Items.Count,
                _session.CompletedRepetitions,
                _session.TotalRepetitions));

        PrevButton.IsEnabled = _index > 0;
        NextButton.IsEnabled = _index < collection.Items.Count - 1;
        RebuildPathDots();
    }

    private void IncrementCount()
    {
        var collection = _session.Collection;
        if (collection is null || collection.Items.Count == 0)
        {
            return;
        }

        var item = collection.Items[_index];
        if (_session.IsItemComplete(item))
        {
            return;
        }

        _session.Increment(item);
        ShowItem();
        ScheduleAutoAdvanceIfNeeded();
    }

    private void ReadingCard_OnTapped(object sender, TappedRoutedEventArgs e)
        => IncrementCount();

    private void ReadingCard_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Visual feedback only; count via Tapped to avoid double-fire.
    }

    private void CountButton_OnClick(object sender, RoutedEventArgs e)
        => IncrementCount();

    private void MarkDoneButton_OnClick(object sender, RoutedEventArgs e)
    {
        var collection = _session.Collection;
        if (collection is null || collection.Items.Count == 0)
        {
            return;
        }

        _session.MarkComplete(collection.Items[_index]);
        ShowItem();
        ScheduleAutoAdvanceIfNeeded();
    }

    private void ResetProgressButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelAutoAdvance();
        _session.Reset();
        ShowItem();
    }

    private void PrevButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_index <= 0)
        {
            return;
        }

        CancelAutoAdvance();
        _index--;
        ShowItem();
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        var collection = _session.Collection;
        if (collection is null || _index >= collection.Items.Count - 1)
        {
            return;
        }

        CancelAutoAdvance();
        _index++;
        ShowItem();
    }

    private void ScheduleAutoAdvanceIfNeeded()
    {
        CancelAutoAdvance();
        var collection = _session.Collection;
        if (collection is null || collection.Items.Count == 0)
        {
            return;
        }

        var item = collection.Items[_index];
        var enabled = App.State?.Settings.AdhkarAutoAdvanceEnabled ?? true;
        if (!AdhkarAutoAdvance.ShouldAdvance(enabled, _session.IsItemComplete(item), _index, collection.Items.Count))
        {
            return;
        }

        _autoAdvanceCts = new CancellationTokenSource();
        var token = _autoAdvanceCts.Token;
        // Brief hold so the checkmark is visible; skip delay when reduce-motion style is forced.
        var delayMs = 400;
        _ = AdvanceAfterDelayAsync(delayMs, token);
    }

    private async Task AdvanceAfterDelayAsync(int delayMs, CancellationToken token)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token).ConfigureAwait(true);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            void MoveNext()
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var collection = _session.Collection;
                if (collection is null || _index >= collection.Items.Count - 1)
                {
                    return;
                }

                _index++;
                ShowItem();
            }

            if (DispatcherQueue.HasThreadAccess)
            {
                MoveNext();
            }
            else
            {
                _ = DispatcherQueue.TryEnqueue(MoveNext);
            }
        }
        catch (TaskCanceledException)
        {
            // user navigated away
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Adhkar auto-advance: {ex.Message}");
        }
    }

    private void CancelAutoAdvance()
    {
        try
        {
            _autoAdvanceCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        _autoAdvanceCts?.Dispose();
        _autoAdvanceCts = null;
    }

    private void MinButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
        {
            p.Minimize();
        }
    }

    private void MaxButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_appWindow?.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter p)
        {
            return;
        }

        if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
        {
            p.Restore();
        }
        else
        {
            p.Maximize();
        }
    }

    private void CloseCaptionButton_OnClick(object sender, RoutedEventArgs e)
        => Close();
}
