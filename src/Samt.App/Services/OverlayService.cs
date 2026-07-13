using Microsoft.UI.Xaml;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt_App.Helpers;
using Samt_App.Overlay;

namespace Samt_App.Services;

/// <summary>
/// Shows / dismisses the prayer overlay window and coordinates stop with audio.
/// Variant A (top) for pre-alert; variant B (bottom dock) for prayer start.
/// </summary>
public sealed class OverlayService : IDisposable
{
    private readonly LocalizationService _localization;
    private readonly AdhanAudioService _audio;
    private OverlayWindow? _window;
    private DispatcherTimer? _autoDismissTimer;
    private bool _disposed;
    private bool _sessionActive;

    public OverlayService(LocalizationService localization, AdhanAudioService audio)
    {
        _localization = localization;
        _audio = audio;
        _audio.PlaybackEnded += OnAudioEnded;
    }

    public bool IsVisible => _sessionActive && _window is not null;

    /// <summary>Show overlay (and optionally play audio) for a planned notification.</summary>
    public void Show(
        PlannedNotification planned,
        string prayerName,
        AudioProfile audioProfile,
        OverlayProfile overlayProfile,
        OverlayVisualStyle? styleOverride = null,
        OverlayEdge? edgeOverride = null,
        double? opacityOverride = null,
        int? animationMsOverride = null,
        bool? forceMotion = null)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            CancelAutoDismiss();
            EnsureWindow();

            var isStart = planned.Kind == PlannedNotificationKind.PrayerStart;
            var timeText = LatinDigits.Time(planned.FireAt);
            var subtitle = isStart
                ? _localization.Get("OverlayPrayerEntered")
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _localization.Get("OverlayPreAlertFormat"),
                    LatinDigits.Number(planned.OffsetMinutes ?? 0));

            var stopLabel = isStart
                ? _localization.Get("OverlayStopAdhan")
                : _localization.Get("OverlayDismiss");

            var style = styleOverride
                        ?? (isStart ? OverlayVisualStyle.BottomDock : OverlayVisualStyle.TopRibbon);

            var edge = edgeOverride
                       ?? (isStart
                           ? (overlayProfile.EntryEdge == OverlayEdge.Top ? OverlayEdge.Bottom : overlayProfile.EntryEdge)
                           : OverlayEdge.Top);

            var opacity = Math.Clamp(opacityOverride ?? overlayProfile.Opacity, 0.30, 1.0);
            if (opacity <= 0)
            {
                opacity = 0.94;
            }

            var durationMs = animationMsOverride
                             ?? (int)Math.Clamp(overlayProfile.AnimationDuration.TotalMilliseconds, 80, 1200);

            // Design-lab previews pass forceMotion=true so OS "reduce motion" does not kill the lab.
            // Live prayer notifications still honor system accessibility.
            var reduceMotion = forceMotion == true
                ? false
                : IsReduceMotionPreferred();

            _window!.Configure(
                prayerName: prayerName,
                timeText: timeText,
                subtitle: subtitle,
                stopLabel: stopLabel,
                style: style,
                entryEdge: edge,
                flowDirection: _localization.FlowDirection,
                opacity: opacity,
                animationMs: durationMs,
                reduceMotion: reduceMotion);

            _window.ShowTopmost();
            _sessionActive = true;

            if (isStart
                && audioProfile.Source != AudioSource.Silent
                && (planned.Channels.HasFlag(NotificationChannel.Audio)
                    || planned.Channels == NotificationChannel.All))
            {
                _audio.Play(audioProfile);
                ScheduleAutoDismiss(TimeSpan.FromMinutes(3));
            }
            else
            {
                ScheduleAutoDismiss(TimeSpan.FromSeconds(isStart ? 12 : 8));
            }

            LaunchLog.Write(
                $"Overlay shown: {planned.Id} style={style} edge={edge} opacity={opacity:0.##} anim={durationMs}ms");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Overlay Show failed: {ex}");
        }
    }

    public void Dismiss(bool stopAudio = true)
    {
        CancelAutoDismiss();
        if (stopAudio)
        {
            _audio.Stop();
        }

        try
        {
            _window?.HideOverlay();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Overlay hide failed: {ex.Message}");
        }

        _sessionActive = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _audio.PlaybackEnded -= OnAudioEnded;
        CancelAutoDismiss();
        try
        {
            _window?.Close();
        }
        catch
        {
            // ignore
        }

        _window = null;
        _sessionActive = false;
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new OverlayWindow();
        _window.StopRequested += (_, _) => Dismiss(stopAudio: true);
        _window.Activate();
        _window.HideOverlay(immediate: true);
    }

    private void OnAudioEnded(object? sender, EventArgs e)
    {
        if (!_sessionActive)
        {
            return;
        }

        var hold = App.State.Settings.DefaultOverlay.PostAudioHold;
        if (hold < TimeSpan.FromSeconds(1))
        {
            hold = TimeSpan.FromSeconds(5);
        }

        ScheduleAutoDismiss(hold);
    }

    private void ScheduleAutoDismiss(TimeSpan delay)
    {
        CancelAutoDismiss();
        _autoDismissTimer = new DispatcherTimer { Interval = delay };
        _autoDismissTimer.Tick += (_, _) =>
        {
            CancelAutoDismiss();
            if (_sessionActive)
            {
                Dismiss(stopAudio: true);
            }
        };
        _autoDismissTimer.Start();
    }

    private void CancelAutoDismiss()
    {
        if (_autoDismissTimer is null)
        {
            return;
        }

        _autoDismissTimer.Stop();
        _autoDismissTimer = null;
    }

    private static bool IsReduceMotionPreferred()
    {
        // NOTE: On this machine / unpackaged WinUI, UISettings.AnimationsEnabled was
        // false and silently killed every overlay entrance (see launch.log reduce=True).
        // Personal v1 always animates; a future settings toggle can reintroduce respect
        // for OS reduce-motion without breaking Design lab.
        return false;
    }
}
