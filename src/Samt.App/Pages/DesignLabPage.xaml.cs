using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Windows.UI;

namespace Samt_App.Pages;

/// <summary>
/// PROTOTYPE — toast/overlay motion + opacity lab. Not production.
/// </summary>
public sealed partial class DesignLabPage : Page
{
    private string _variant = "A";
    private string _edge = "Top";
    private double _opacityPercent = 92;
    private double _durationMs = 320;

    public DesignLabPage()
    {
        InitializeComponent();
        // Set slider ranges in code (Maximum first). XAML Minimum before Maximum (or culture parse) throws.
        ConfigureSliders();
        Loaded += (_, _) =>
        {
            FlowDirection = App.Localization.FlowDirection;
            LabLabel.Text = App.Localization.Get("NavDesignLab") + " — toast motion";
            ApplyVariantVisuals();
            ApplyOpacity();
            // Park off-stage until Play.
            ResetTransformOffStage();
        };
    }

    private void ConfigureSliders()
    {
        OpacitySlider.Maximum = 100;
        OpacitySlider.Minimum = 30;
        OpacitySlider.StepFrequency = 1;
        OpacitySlider.Value = 92;

        DurationSlider.Maximum = 900;
        DurationSlider.Minimum = 150;
        DurationSlider.StepFrequency = 10;
        DurationSlider.Value = 320;

        OpacityValueText.Text = LatinDigits.Number(92) + "%";
        DurationValueText.Text = LatinDigits.Number(320) + "ms";
    }

    private void VariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            _variant = tag;
            ApplyVariantVisuals();
            PlayEntrance();
        }
    }

    private void EdgeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EdgeBox.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            _edge = tag;
        }
    }

    private void OpacitySlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _opacityPercent = e.NewValue;
        OpacityValueText.Text = LatinDigits.Number((int)_opacityPercent) + "%";
        ApplyOpacity();
    }

    private void DurationSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _durationMs = e.NewValue;
        DurationValueText.Text = LatinDigits.Number((int)_durationMs) + "ms";
    }

    private void PlayBtn_OnClick(object sender, RoutedEventArgs e) => PlayEntrance();

    private void StopBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var (dx, dy) = EdgeOffset();
        var duration = TimeSpan.FromMilliseconds(Math.Max(140, _durationMs * 0.55));

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(ToastCard);
            var compositor = visual.Compositor;
            // Ease-in dismiss: cubic-bezier(0.4, 0, 0.7, 0)
            var easeIn = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.40f, 0.0f), new Vector2(0.70f, 0.0f));

            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0f, visual.Offset);
            move.InsertKeyFrame(1f, new Vector3((float)dx, (float)dy, 0), easeIn);
            move.Duration = duration;
            move.StopBehavior = AnimationStopBehavior.SetToFinalValue;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, visual.Opacity);
            fade.InsertKeyFrame(1f, 0f, easeIn);
            fade.Duration = duration;
            fade.StopBehavior = AnimationStopBehavior.SetToFinalValue;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (_, _) =>
            {
                ResetTransformOffStage();
                visual.Offset = Vector3.Zero;
                visual.Opacity = (float)Math.Clamp(_opacityPercent / 100.0, 0.3, 1.0);
                ApplyOpacity();
            };
            visual.StartAnimation("Offset", move);
            visual.StartAnimation("Opacity", fade);
            batch.End();
        }
        catch
        {
            ResetTransformOffStage();
            ApplyOpacity();
        }
    }

    private void FireToastBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var title = App.Localization.GetPrayerName(PrayerEvent.Fajr);
            var body = LatinDigits.Time(DateTimeOffset.Now) + " - Design lab";
            ToastSubtitle.Text = "Toast test: " + body;
            PlayEntrance();
            App.ShowSamplePrayerToast(title, body);
            Helpers.LaunchLog.Write($"DesignLab toast test: {title} {body}");
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"DesignLab toast test failed: {ex.Message}");
        }
    }

    private void FireOverlayBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToastSubtitle.Text =
                $"Production overlay · opacity {LatinDigits.Number((int)_opacityPercent)}% · {LatinDigits.Number((int)_durationMs)}ms";
            PlayEntrance();
            App.PreviewPrayerChannels(
                prayerStart: true,
                opacity: Math.Clamp(_opacityPercent / 100.0, 0.30, 1.0),
                animationMs: (int)_durationMs,
                edgeTag: _edge,
                variantTag: string.IsNullOrEmpty(_variant) ? "B" : _variant);
            Helpers.LaunchLog.Write(
                $"DesignLab overlay+audio opacity={_opacityPercent} duration={_durationMs} edge={_edge} variant={_variant}");
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"DesignLab overlay preview failed: {ex.Message}");
        }
    }

    private void FirePreAlertBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToastSubtitle.Text =
                $"Pre-alert · opacity {LatinDigits.Number((int)_opacityPercent)}% · {LatinDigits.Number((int)_durationMs)}ms";
            PlayEntrance();
            App.PreviewPrayerChannels(
                prayerStart: false,
                opacity: Math.Clamp(_opacityPercent / 100.0, 0.30, 1.0),
                animationMs: (int)_durationMs,
                edgeTag: _edge,
                variantTag: string.IsNullOrEmpty(_variant) ? "A" : _variant);
            Helpers.LaunchLog.Write(
                $"DesignLab pre-alert opacity={_opacityPercent} duration={_durationMs} edge={_edge} variant={_variant}");
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"DesignLab pre-alert preview failed: {ex.Message}");
        }
    }

    private void ApplyVariantVisuals()
    {
        // Reset light/dark text colors (C used to flip to ivory card).
        ToastEyebrow.Foreground = (Brush)Application.Current.Resources["SamtGoldSoftBrush"];
        ToastTitle.Foreground = (Brush)Application.Current.Resources["SamtIvoryBrush"];
        ToastTime.Foreground = (Brush)Application.Current.Resources["SamtGoldBrightBrush"];
        ToastSubtitle.Foreground = (Brush)Application.Current.Resources["SamtIvoryBrush"];

        switch (_variant)
        {
            case "B": // Bottom dock — largest type
                ToastCard.VerticalAlignment = VerticalAlignment.Bottom;
                ToastCard.HorizontalAlignment = HorizontalAlignment.Stretch;
                ToastCard.Margin = new Thickness(32, 0, 32, 48);
                ToastCard.CornerRadius = new CornerRadius(20);
                ToastCard.MaxWidth = 720;
                ToastTitle.Text = "المغرب";
                ToastTime.Text = "18:28";
                ToastSubtitle.Text = "دخل وقت الصلاة";
                StopBtn.Content = "إيقاف الأذان";
                ToastBgBrush.Color = Color.FromArgb(255, 0x07, 0x15, 0x25);
                ApplyTypeScale(eyebrow: 12, title: 34, time: 38, subtitle: 16, stop: 15, stackGap: 6, padX: 26, padY: 20);
                break;
            case "C": // Edge — slim compact type
                ToastCard.VerticalAlignment = VerticalAlignment.Center;
                ToastCard.HorizontalAlignment = FlowDirection == FlowDirection.RightToLeft
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
                ToastCard.Margin = new Thickness(16, 0, 16, 0);
                ToastCard.CornerRadius = new CornerRadius(16);
                ToastCard.MaxWidth = 340;
                ToastTitle.Text = "العشاء";
                ToastTime.Text = "20:05";
                ToastSubtitle.Text = "اضغط Esc للإيقاف";
                StopBtn.Content = "Esc";
                ToastBgBrush.Color = Color.FromArgb(255, 0x07, 0x15, 0x25);
                ApplyTypeScale(eyebrow: 10, title: 20, time: 24, subtitle: 12, stop: 12, stackGap: 3, padX: 16, padY: 12);
                break;
            default: // A Top — compact ribbon
                ToastCard.VerticalAlignment = VerticalAlignment.Top;
                ToastCard.HorizontalAlignment = HorizontalAlignment.Center;
                ToastCard.Margin = new Thickness(24, 40, 24, 0);
                ToastCard.CornerRadius = new CornerRadius(14);
                ToastCard.MaxWidth = 420;
                ToastTitle.Text = "الفجر";
                ToastTime.Text = "05:12";
                ToastSubtitle.Text = "دخل وقت الصلاة";
                StopBtn.Content = "إيقاف";
                ToastBgBrush.Color = Color.FromArgb(255, 0x0B, 0x1F, 0x33);
                ApplyTypeScale(eyebrow: 10, title: 22, time: 26, subtitle: 13, stop: 13, stackGap: 3, padX: 18, padY: 12);
                break;
        }

        ApplyOpacity();
    }

    private void ApplyTypeScale(
        double eyebrow,
        double title,
        double time,
        double subtitle,
        double stop,
        double stackGap,
        double padX,
        double padY)
    {
        ToastEyebrow.FontSize = eyebrow;
        ToastTitle.FontSize = title;
        ToastTime.FontSize = time;
        ToastSubtitle.FontSize = subtitle;
        StopBtn.FontSize = stop;
        ToastTextStack.Spacing = stackGap;
        ToastContentPanel.Padding = new Thickness(padX, padY, padX, padY);
        StopBtn.MinWidth = stop >= 15 ? 120 : stop >= 13 ? 96 : 84;
        StopBtn.MinHeight = stop >= 15 ? 46 : stop >= 13 ? 36 : 34;
        var px = stop >= 15 ? 20 : 14;
        var py = stop >= 15 ? 10 : 8;
        StopBtn.Padding = new Thickness(px, py, px, py);
    }

    private void ApplyOpacity()
    {
        var o = Math.Clamp(_opacityPercent / 100.0, 0.3, 1.0);
        ToastCard.Opacity = o;
        ToastBgBrush.Opacity = o;
    }

    private void ResetTransformOffStage()
    {
        var (dx, dy) = EdgeOffset();
        ToastTransform.TranslateX = dx;
        ToastTransform.TranslateY = dy;
        ToastCard.Opacity = 0;
    }

    private (double dx, double dy) EdgeOffset()
    {
        const double dist = 90;
        return _edge switch
        {
            "Bottom" => (0, dist),
            "Start" => FlowDirection == FlowDirection.RightToLeft ? (dist, 0) : (-dist, 0),
            "End" => FlowDirection == FlowDirection.RightToLeft ? (-dist, 0) : (dist, 0),
            _ => (0, -dist) // Top
        };
    }

    private void PlayEntrance()
    {
        var (dx, dy) = EdgeOffset();
        var targetOpacity = Math.Clamp(_opacityPercent / 100.0, 0.3, 1.0);
        var duration = TimeSpan.FromMilliseconds(_durationMs);

        // Keep XAML transform in sync for Stop / Reset paths.
        ToastTransform.TranslateX = 0;
        ToastTransform.TranslateY = 0;
        ToastBgBrush.Opacity = 1;

        // Composition animations are independent and reliable on WinUI 3 (Storyboard
        // transform paths often no-op without special setup).
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(ToastCard);
            var compositor = visual.Compositor;

            visual.Offset = new Vector3((float)dx, (float)dy, 0);
            visual.Opacity = 0f;

            // Match production: cubic-bezier(0.16, 1, 0.3, 1) ease-out for position.
            var easeOut = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.16f, 1.0f), new Vector2(0.30f, 1.0f));
            // Opacity: cubic-bezier(0.2, 0, 0, 1)
            var easeOpacity = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.20f, 0.0f), new Vector2(0.0f, 1.0f));

            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0f, new Vector3((float)dx, (float)dy, 0));
            move.InsertKeyFrame(1f, Vector3.Zero, easeOut);
            move.Duration = duration;
            move.StopBehavior = AnimationStopBehavior.SetToFinalValue;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, (float)targetOpacity, easeOpacity);
            fade.Duration = duration;
            fade.StopBehavior = AnimationStopBehavior.SetToFinalValue;

            visual.StartAnimation("Offset", move);
            visual.StartAnimation("Opacity", fade);
            Helpers.LaunchLog.Write($"DesignLab Play ease-out dx={dx} dy={dy} opacity={targetOpacity:0.##} dur={_durationMs}");
        }
        catch (Exception ex)
        {
            Helpers.LaunchLog.Write($"DesignLab composition play failed, storyboard fallback: {ex.Message}");
            // Fallback storyboard
            ToastTransform.TranslateX = dx;
            ToastTransform.TranslateY = dy;
            ToastCard.Opacity = 0;
            var story = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 0,
                To = targetOpacity,
                Duration = duration,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fade, ToastCard);
            Storyboard.SetTargetProperty(fade, "Opacity");
            story.Children.Add(fade);
            story.Begin();
        }
    }
}
