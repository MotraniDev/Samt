using System.ComponentModel;
using System.Runtime.CompilerServices;
using Samt.Core.Domain;
using Samt.Core.Storage;
using Samt.Core.Time;

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
        _settings = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        _loaded = true;
        RaiseAll();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(_settings, cancellationToken).ConfigureAwait(true);
    }

    public async Task UpdateAsync(Func<AppSettings, AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        _settings = SettingsJson.Normalize(mutate(_settings));
        await _store.SaveAsync(_settings, cancellationToken).ConfigureAwait(true);
        RaiseAll();
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
