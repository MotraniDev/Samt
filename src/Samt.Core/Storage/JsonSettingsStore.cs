using Samt.Core.Domain;

namespace Samt.Core.Storage;

/// <summary>
/// Atomic JSON settings store with <c>settings.bak</c> last-good backup.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
        _backupPath = Path.Combine(directory, "settings.bak");
    }

    public string SettingsPath => _settingsPath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
                    return SettingsJson.Deserialize(json);
                }
                catch
                {
                    // Fall through to backup / defaults.
                }
            }

            if (File.Exists(_backupPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_backupPath, cancellationToken).ConfigureAwait(false);
                    var settings = SettingsJson.Deserialize(json);
                    // Restore primary from backup
                    await WriteAtomicAsync(SettingsJson.Serialize(settings), cancellationToken).ConfigureAwait(false);
                    return settings;
                }
                catch
                {
                    // Fall through to defaults.
                }
            }

            var defaults = SettingsJson.CreateDefault();
            await WriteAtomicAsync(SettingsJson.Serialize(defaults), cancellationToken).ConfigureAwait(false);
            return defaults;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = SettingsJson.Normalize(settings);
        var json = SettingsJson.Serialize(normalized);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAtomicAsync(json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAtomicAsync(string json, CancellationToken cancellationToken)
    {
        var tempPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

        // Preserve previous good file as backup before replacing primary.
        if (File.Exists(_settingsPath))
        {
            File.Copy(_settingsPath, _backupPath, overwrite: true);
        }

        File.Move(tempPath, _settingsPath, overwrite: true);

        // First successful write: seed backup so a corrupt primary can recover.
        if (!File.Exists(_backupPath))
        {
            File.Copy(_settingsPath, _backupPath, overwrite: true);
        }
    }
}
