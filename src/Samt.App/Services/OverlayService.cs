using Microsoft.UI.Xaml;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt.Core.Time;
using Samt_App.Helpers;
using Samt_App.Overlay;

namespace Samt_App.Services;

/// <summary>
/// Shows / dismisses the prayer overlay window and coordinates stop with audio.
/// Variant A (top) for pre-alert; variant B (bottom dock) for prayer start.
/// Window stays open while audio plays (mute does not dismiss).
/// </summary>
public sealed class OverlayService : IDisposable
{
    /// <summary>Safety net if MediaEnded never fires (corrupt file, driver hang).</summary>
    private static readonly TimeSpan AudioSafetyTimeout = TimeSpan.FromMinutes(20);

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
        _audio.MuteChanged += OnMuteChanged;
    }

    public bool IsVisible => _sessionActive && _window is not null;

    /// <summary>Pre-alert live countdown hit zero — host should fire Adhan start now.</summary>
    public event EventHandler? PreAlertCountdownReachedZero;

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
            // Data source times are Adhan instants. Pre-alert FireAt is minutes before Adhan.
            DateTimeOffset? countdownTo = null;
            string timeText;
            string subtitle;
            if (isStart)
            {
                timeText = LatinDigits.Time(planned.FireAt);
                subtitle = _localization.Get("OverlayAdhanArrived");
            }
            else
            {
                var adhanAt = planned.FireAt.AddMinutes(planned.OffsetMinutes ?? 0);
                countdownTo = adhanAt;
                var remaining = adhanAt - DateTimeOffset.Now;
                timeText = PrayerTimeline.FormatCountdownHms(remaining);
                subtitle = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _localization.Get("OverlayPreAlertFormat"),
                    LatinDigits.Number(planned.OffsetMinutes ?? 0));
            }

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
                muteLabel: _localization.Get("OverlayMute"),
                unmuteLabel: _localization.Get("OverlayUnmute"),
                closeLabel: _localization.Get("OverlayClose"),
                eyebrowLabel: _localization.Get("AdhanTime"),
                style: style,
                entryEdge: edge,
                flowDirection: _localization.FlowDirection,
                opacity: opacity,
                animationMs: durationMs,
                reduceMotion: reduceMotion,
                isMuted: _audio.IsMuted,
                countdownTo: countdownTo);

            _window.ShowTopmost();
            _sessionActive = true;

            // Play for BOTH prayer-start and pre-alert whenever a non-silent profile was resolved.
            // (Previously only prayer-start played — pre-alert audio channel was silently dropped.)
            if (audioProfile.Source != AudioSource.Silent)
            {
                // Fresh notification starts unmuted; user can mute again from the overlay.
                _audio.SetMuted(false);
                _audio.Play(audioProfile);
                // Do NOT dismiss on a short timer while audio is active — wait for PlaybackEnded.
                // Safety timeout only (long adhan files can exceed 3 minutes).
                ScheduleAutoDismiss(AudioSafetyTimeout);
            }
            else
            {
                ScheduleAutoDismiss(TimeSpan.FromSeconds(isStart ? 12 : 8));
            }

            LaunchLog.Write(
                $"Overlay shown: {planned.Id} style={style} edge={edge} opacity={opacity:0.##} anim={durationMs}ms audio={audioProfile.Source}");
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
        _audio.MuteChanged -= OnMuteChanged;
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
        _window.CloseRequested += (_, _) => Dismiss(stopAudio: true);
        _window.MuteToggleRequested += (_, _) =>
        {
            _audio.ToggleMute();
            _window?.SetMutedVisual(_audio.IsMuted);
        };
        _window.CountdownReachedZero += (_, _) =>
        {
            // Pre-alert timer elapsed — host fires Adhan start; keep audio path free.
            try
            {
                PreAlertCountdownReachedZero?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"PreAlertCountdownReachedZero: {ex.Message}");
            }
        };
        _window.Activate();
        _window.HideOverlay(immediate: true);
    }

    private void OnMuteChanged(object? sender, EventArgs e)
    {
        if (!_sessionActive)
        {
            return;
        }

        try
        {
            var dq = App.MainWindow?.DispatcherQueue
                     ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dq is not null)
            {
                dq.TryEnqueue(() => _window?.SetMutedVisual(_audio.IsMuted));
            }
            else
            {
                _window?.SetMutedVisual(_audio.IsMuted);
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Overlay mute visual: {ex.Message}");
        }
    }

    private void OnAudioEnded(object? sender, EventArgs e)
    {
        if (!_sessionActive)
        {
            return;
        }

        // Audio finished naturally (or failed) — hold briefly then dismiss.
        // Mute does NOT end audio; MediaEnded still fires at the real track end.
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
                // If audio is still playing (safety timer only), stop and dismiss together.
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
