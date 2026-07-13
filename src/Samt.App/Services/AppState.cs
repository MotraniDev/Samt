using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Samt.Core.Domain;
using Samt.Core.Storage;
using Samt.Core.Time;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>In-memory app settings with durable persistence.</summary>
public sealed class AppState : INotifyPropertyChanged
{
    private readonly ISettingsStore _store;
    private readonly IClock _clock;
    private AppSettings _settings = SettingsJson.CreateDefault();
    private bool _loaded;

    public AppState(ISettingsStore store, IClock? clock = null)
    {
        _store = store;
        _clock = clock ?? new SystemClock();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsChanged;

    public AppSettings Settings => _settings;

    public bool IsLoaded => _loaded;

    public DateTimeOffset Now => _clock.UtcNow;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // File I/O off UI; notifications always on UI (WinUI often has no SynchronizationContext).
        _settings = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _loaded = true;
        await RaiseAllOnUiAsync().ConfigureAwait(false);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Func<AppSettings, AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        try
        {
            _settings = SettingsJson.Normalize(mutate(_settings));
            await _store.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
            // Subscribers (Today/Locations ObservableCollections, tray) must run on the UI thread.
            await RaiseAllOnUiAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never let settings I/O take down the process.
            LaunchLog.Write($"AppState.UpdateAsync failed: {ex}");
            throw;
        }
    }

    public LocationProfile? TryGetActiveLocation()
        => _settings.GetActiveLocation();

    public LocationProfile RequireActiveLocation()
        => _settings.GetActiveLocation()
           ?? throw new InvalidOperationException("No active location configured.");

    public CalculationProfile RequireCalculationProfile()
        => _settings.GetActiveCalculationProfile();

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Settings));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raise change notifications on the UI dispatcher. WinUI 3 often has no
    /// <see cref="SynchronizationContext"/>, so ConfigureAwait(true) alone does not
    /// return to the UI thread after async file I/O — and binding updates crash/hang.
    /// </summary>
    private Task RaiseAllOnUiAsync()
    {
        var dq = DispatcherQueue.GetForCurrentThread()
                 ?? App.MainWindow?.DispatcherQueue;

        if (dq is null || dq.HasThreadAccess)
        {
            RaiseAll();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dq.TryEnqueue(() =>
            {
                try
                {
                    RaiseAll();
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            // Queue rejected — raise here rather than drop the update silently.
            RaiseAll();
            return Task.CompletedTask;
        }

        return tcs.Task;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
