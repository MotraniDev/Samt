using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        // Quick dismiss animation.
        var story = new Storyboard();
        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(Math.Max(120, _durationMs * 0.6)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fade, ToastCard);
        Storyboard.SetTargetProperty(fade, "Opacity");
        story.Children.Add(fade);
        story.Completed += (_, _) =>
        {
            ResetTransformOffStage();
            ApplyOpacity();
        };
        story.Begin();
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

    private void ApplyVariantVisuals()
    {
        switch (_variant)
        {
            case "B":
                ToastCard.VerticalAlignment = VerticalAlignment.Bottom;
                ToastCard.HorizontalAlignment = HorizontalAlignment.Stretch;
                ToastCard.Margin = new Thickness(32, 0, 32, 48);
                ToastCard.CornerRadius = new CornerRadius(24);
                ToastTitle.Text = "المغرب";
                ToastTime.Text = "18:28";
                ToastSubtitle.Text = "دخل وقت الصلاة";
                StopBtn.Content = "إيقاف الأذان";
                ToastBgBrush.Color = Color.FromArgb(255, 0x07, 0x15, 0x25);
                break;
            case "C":
                ToastCard.VerticalAlignment = VerticalAlignment.Center;
                ToastCard.HorizontalAlignment = FlowDirection == FlowDirection.RightToLeft
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
                ToastCard.Margin = new Thickness(0);
                ToastCard.CornerRadius = new CornerRadius(0, 16, 16, 0);
                ToastTitle.Text = "العشاء";
                ToastTime.Text = "20:05";
                ToastSubtitle.Text = "اضغط Esc للإيقاف";
                StopBtn.Content = "Esc";
                ToastBgBrush.Color = Color.FromArgb(255, 0xF4, 0xEF, 0xE4);
                ToastTitle.Foreground = (Brush)Application.Current.Resources["SamtNavyBrush"];
                ToastTime.Foreground = (Brush)Application.Current.Resources["SamtGreenBrush"];
                ToastSubtitle.Foreground = (Brush)Application.Current.Resources["SamtNavyBrush"];
                break;
            default:
                ToastCard.VerticalAlignment = VerticalAlignment.Top;
                ToastCard.HorizontalAlignment = HorizontalAlignment.Center;
                ToastCard.Margin = new Thickness(24, 40, 24, 0);
                ToastCard.CornerRadius = new CornerRadius(14);
                ToastTitle.Text = "الفجر";
                ToastTime.Text = "05:12";
                ToastSubtitle.Text = "دخل وقت الصلاة";
                StopBtn.Content = "إيقاف";
                ToastBgBrush.Color = Color.FromArgb(255, 0x0B, 0x1F, 0x33);
                ToastTitle.Foreground = (Brush)Application.Current.Resources["SamtIvoryBrush"];
                ToastTime.Foreground = (Brush)Application.Current.Resources["SamtGoldSoftBrush"];
                ToastSubtitle.Foreground = (Brush)Application.Current.Resources["SamtIvoryBrush"];
                break;
        }

        ApplyOpacity();
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
        ResetTransformOffStage();
        ApplyOpacity(); // sets target opacity; start from 0 then animate to it
        var targetOpacity = Math.Clamp(_opacityPercent / 100.0, 0.3, 1.0);
        ToastCard.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(_durationMs);
        var story = new Storyboard();

        var moveX = new DoubleAnimation
        {
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(moveX, ToastTransform);
        Storyboard.SetTargetProperty(moveX, "TranslateX");
        story.Children.Add(moveX);

        var moveY = new DoubleAnimation
        {
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(moveY, ToastTransform);
        Storyboard.SetTargetProperty(moveY, "TranslateY");
        story.Children.Add(moveY);

        var fade = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, ToastCard);
        Storyboard.SetTargetProperty(fade, "Opacity");
        story.Children.Add(fade);

        story.Begin();
    }
}
