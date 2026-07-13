using Windows.Media.Core;
using Windows.Media.Playback;
using Samt.Core.Domain;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Plays adhan / alert audio for prayer notifications.
/// Supports WindowsDefault (generated tone), Bundled asset, LocalFile, or Silent.
/// </summary>
public sealed class AdhanAudioService : IDisposable
{
    private MediaPlayer? _player;
    private bool _disposed;
    private static string? _defaultTonePath;

    public event EventHandler? PlaybackEnded;
    public event EventHandler? PlaybackStarted;

    public bool IsPlaying { get; private set; }

    public void Play(AudioProfile? profile)
    {
        profile ??= new AudioProfile();
        if (profile.Source == AudioSource.Silent)
        {
            Stop();
            return;
        }

        try
        {
            StopInternal(raiseEnded: false);

            var path = ResolvePath(profile, App.State?.Settings);
            if (path is null)
            {
                LaunchLog.Write("AdhanAudio: no playable path; skipping");
                return;
            }

            _player = new MediaPlayer
            {
                IsLoopingEnabled = profile.Loop,
                AudioCategory = MediaPlayerAudioCategory.Media
            };
            _player.MediaEnded += OnMediaEnded;
            _player.MediaFailed += OnMediaFailed;
            var uri = path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? new Uri(path)
                : new Uri(path, UriKind.Absolute);
            _player.Source = MediaSource.CreateFromUri(uri);
            _player.Play();
            IsPlaying = true;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
            LaunchLog.Write($"AdhanAudio playing: {path}");
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            LaunchLog.Write($"AdhanAudio play failed: {ex.Message}");
            DisposePlayer();
        }
    }

    public void Stop() => StopInternal(raiseEnded: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopInternal(raiseEnded: false);
    }

    private void StopInternal(bool raiseEnded)
    {
        var wasPlaying = IsPlaying;
        try
        {
            if (_player is not null)
            {
                _player.MediaEnded -= OnMediaEnded;
                _player.MediaFailed -= OnMediaFailed;
                try { _player.Pause(); } catch { /* ignore */ }
                try { _player.Source = null; } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"AdhanAudio stop: {ex.Message}");
        }
        finally
        {
            DisposePlayer();
            IsPlaying = false;
            if (raiseEnded && wasPlaying)
            {
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void DisposePlayer()
    {
        try
        {
            _player?.Dispose();
        }
        catch
        {
            // ignore
        }

        _player = null;
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        IsPlaying = false;
        try
        {
            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                      ?? App.MainWindow?.DispatcherQueue;
            if (dq is not null)
            {
                dq.TryEnqueue(() => PlaybackEnded?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        LaunchLog.Write($"AdhanAudio MediaFailed: {args.ErrorMessage}");
        IsPlaying = false;
        try
        {
            var dq = App.MainWindow?.DispatcherQueue;
            dq?.TryEnqueue(() => PlaybackEnded?.Invoke(this, EventArgs.Empty));
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Resolve a file:// or absolute path for the given profile.</summary>
    public static string? ResolvePath(AudioProfile profile, AppSettings? settings = null)
    {
        switch (profile.Source)
        {
            case AudioSource.Silent:
                return null;

            case AudioSource.Library:
            {
                var id = profile.SoundId;
                if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(profile.FilePath))
                {
                    id = profile.FilePath;
                }

                settings ??= TryCurrentSettings();
                return SoundLibraryService.ResolvePath(id, settings ?? new AppSettings());
            }

            case AudioSource.LocalFile:
                if (!string.IsNullOrWhiteSpace(profile.FilePath) && File.Exists(profile.FilePath))
                {
                    return Path.GetFullPath(profile.FilePath);
                }

                LaunchLog.Write($"AdhanAudio LocalFile missing: {profile.FilePath}");
                return EnsureDefaultTonePath();

            case AudioSource.Bundled:
            {
                // Prefer library id if present; else legacy single adhan.* under Assets/Audio.
                if (!string.IsNullOrWhiteSpace(profile.SoundId))
                {
                    settings ??= TryCurrentSettings();
                    return SoundLibraryService.ResolvePath(profile.SoundId, settings ?? new AppSettings());
                }

                var bundled = FindBundledAdhan();
                if (bundled is not null)
                {
                    return bundled;
                }

                // Fall through to default library adhan.
                settings ??= TryCurrentSettings();
                var fromLib = SoundLibraryService.ResolvePath(
                    BuiltInSoundIds.AdhanAlaqsa,
                    settings ?? new AppSettings());
                if (fromLib is not null)
                {
                    return fromLib;
                }

                LaunchLog.Write("AdhanAudio bundled asset missing; falling back to default tone");
                return EnsureDefaultTonePath();
            }

            default: // WindowsDefault
                return EnsureDefaultTonePath();
        }
    }

    private static AppSettings? TryCurrentSettings()
    {
        try
        {
            return App.State?.Settings;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindBundledAdhan()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "Assets", "Audio", "library", "adhan-alaqsa.mp3"),
            Path.Combine(baseDir, "Assets", "Audio", "adhan.mp3"),
            Path.Combine(baseDir, "Assets", "Audio", "adhan.wav"),
            Path.Combine(baseDir, "Assets", "Audio", "adhan.m4a")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Short soft tone used when no licensed adhan file is present.
    /// Written once under LocalAppData\SAMT\tones.
    /// </summary>
    internal static string EnsureDefaultTonePath()
    {
        if (_defaultTonePath is not null && File.Exists(_defaultTonePath))
        {
            return _defaultTonePath;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SAMT",
            "tones");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "prayer-alert.wav");
        if (!File.Exists(path))
        {
            WriteSoftAlertWav(path);
        }

        _defaultTonePath = path;
        return path;
    }

    /// <summary>≈2.4s dual-tone WAV (soft alert, not an adhan imitation).</summary>
    private static void WriteSoftAlertWav(string path)
    {
        const int sampleRate = 22050;
        const double durationSec = 2.4;
        var sampleCount = (int)(sampleRate * durationSec);
        var data = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            // Two gentle pulses at ~523 Hz / 659 Hz with envelope.
            var pulse = t < 1.1 ? 1.0 : (t < 1.25 ? 0.0 : 1.0);
            var env = pulse * Math.Sin(Math.PI * Math.Min(t % 1.2, 1.0));
            env = Math.Clamp(env, 0, 1) * 0.22;
            var freq = t < 1.2 ? 523.25 : 659.25;
            var sample = env * Math.Sin(2 * Math.PI * freq * t);
            data[i] = (short)(sample * short.MaxValue);
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        var byteCount = data.Length * 2;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + byteCount);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(byteCount);
        foreach (var s in data)
        {
            bw.Write(s);
        }
    }
}
