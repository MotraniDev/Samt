using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Notifications;
using Samt_App.Helpers;
using Samt_App.Services;

namespace Samt_App.ViewModels;

/// <summary>Edits general + per-prayer notification rules and sound library picks.</summary>
public sealed class AlertsViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly LocalizationService _localization;
    private readonly AdhanAudioService _previewAudio = new();
    private bool _suppress;
    private bool _beforeEnabled = true;
    private bool _startEnabled = true;
    private string _generalBeforeMinutes = "15";
    private string _fajrException = "30";
    private string _dhuhrException = string.Empty;
    private string _asrException = string.Empty;
    private string _maghribException = string.Empty;
    private string _ishaException = string.Empty;
    private bool _beforeToast = true;
    private bool _beforeOverlay = true;
    private bool _startToast = true;
    private bool _startOverlay = true;
    private bool _startAudio = true;
    private bool _startFajr = true;
    private bool _startDhuhr = true;
    private bool _startAsr = true;
    private bool _startMaghrib = true;
    private bool _startIsha = true;
    private bool _beforeFajr = true;
    private bool _beforeDhuhr = true;
    private bool _beforeAsr = true;
    private bool _beforeMaghrib = true;
    private bool _beforeIsha = true;
    private string _statusMessage = string.Empty;
    private string _priorityNote = string.Empty;
    private SoundPickItem? _selectedAdhanSound;
    private SoundPickItem? _selectedPreAlertSound;

    public AlertsViewModel(AppState appState, LocalizationService localization)
    {
        _appState = appState;
        _localization = localization;
        _appState.SettingsChanged += (_, _) =>
        {
            if (!_suppress)
            {
                LoadFromSettings();
            }
        };
        LoadFromSettings();
    }

    public ObservableCollection<SoundPickItem> AdhanSoundOptions { get; } = [];
    public ObservableCollection<SoundPickItem> PreAlertSoundOptions { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool BeforeEnabled
    {
        get => _beforeEnabled;
        set => Set(ref _beforeEnabled, value);
    }

    public bool StartEnabled
    {
        get => _startEnabled;
        set => Set(ref _startEnabled, value);
    }

    public string GeneralBeforeMinutes
    {
        get => _generalBeforeMinutes;
        set => Set(ref _generalBeforeMinutes, LatinDigits.EnsureLatin(value));
    }

    public string FajrException
    {
        get => _fajrException;
        set => Set(ref _fajrException, LatinDigits.EnsureLatin(value));
    }

    public string DhuhrException
    {
        get => _dhuhrException;
        set => Set(ref _dhuhrException, LatinDigits.EnsureLatin(value));
    }

    public string AsrException
    {
        get => _asrException;
        set => Set(ref _asrException, LatinDigits.EnsureLatin(value));
    }

    public string MaghribException
    {
        get => _maghribException;
        set => Set(ref _maghribException, LatinDigits.EnsureLatin(value));
    }

    public string IshaException
    {
        get => _ishaException;
        set => Set(ref _ishaException, LatinDigits.EnsureLatin(value));
    }

    public bool BeforeToast
    {
        get => _beforeToast;
        set => Set(ref _beforeToast, value);
    }

    public bool BeforeOverlay
    {
        get => _beforeOverlay;
        set => Set(ref _beforeOverlay, value);
    }

    public bool StartToast
    {
        get => _startToast;
        set => Set(ref _startToast, value);
    }

    public bool StartOverlay
    {
        get => _startOverlay;
        set => Set(ref _startOverlay, value);
    }

    public bool StartAudio
    {
        get => _startAudio;
        set => Set(ref _startAudio, value);
    }

    public bool StartFajr { get => _startFajr; set => Set(ref _startFajr, value); }
    public bool StartDhuhr { get => _startDhuhr; set => Set(ref _startDhuhr, value); }
    public bool StartAsr { get => _startAsr; set => Set(ref _startAsr, value); }
    public bool StartMaghrib { get => _startMaghrib; set => Set(ref _startMaghrib, value); }
    public bool StartIsha { get => _startIsha; set => Set(ref _startIsha, value); }

    public bool BeforeFajr { get => _beforeFajr; set => Set(ref _beforeFajr, value); }
    public bool BeforeDhuhr { get => _beforeDhuhr; set => Set(ref _beforeDhuhr, value); }
    public bool BeforeAsr { get => _beforeAsr; set => Set(ref _beforeAsr, value); }
    public bool BeforeMaghrib { get => _beforeMaghrib; set => Set(ref _beforeMaghrib, value); }
    public bool BeforeIsha { get => _beforeIsha; set => Set(ref _beforeIsha, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            var next = LatinDigits.EnsureLatin(value ?? string.Empty);
            if (_statusMessage == next)
            {
                return;
            }

            _statusMessage = next;
            OnPropertyChanged();
        }
    }

    public string PriorityNote
    {
        get => _priorityNote;
        private set
        {
            if (_priorityNote == value)
            {
                return;
            }

            _priorityNote = value;
            OnPropertyChanged();
        }
    }

    public SoundPickItem? SelectedAdhanSound
    {
        get => _selectedAdhanSound;
        set
        {
            if (ReferenceEquals(_selectedAdhanSound, value)
                || (_selectedAdhanSound?.Id == value?.Id && value is not null))
            {
                if (!ReferenceEquals(_selectedAdhanSound, value) && value is not null)
                {
                    _selectedAdhanSound = value;
                    OnPropertyChanged();
                }

                return;
            }

            _selectedAdhanSound = value;
            OnPropertyChanged();
        }
    }

    public SoundPickItem? SelectedPreAlertSound
    {
        get => _selectedPreAlertSound;
        set
        {
            if (ReferenceEquals(_selectedPreAlertSound, value)
                || (_selectedPreAlertSound?.Id == value?.Id && value is not null))
            {
                if (!ReferenceEquals(_selectedPreAlertSound, value) && value is not null)
                {
                    _selectedPreAlertSound = value;
                    OnPropertyChanged();
                }

                return;
            }

            _selectedPreAlertSound = value;
            OnPropertyChanged();
        }
    }

    public void RefreshLabels()
    {
        PriorityNote = _localization.Get("AlertsPriorityNote");
        RebuildSoundOptions();
    }

    public void LoadFromSettings()
    {
        _suppress = true;
        try
        {
            var model = NotificationRulesComposer.Parse(_appState.Settings.NotificationRules);
            BeforeEnabled = model.BeforeAlertsEnabled;
            StartEnabled = model.StartAlertsEnabled;
            GeneralBeforeMinutes = LatinDigits.Number(model.GeneralBeforeMinutes);
            BeforeToast = model.BeforeChannels.HasFlag(NotificationChannel.WindowsToast);
            BeforeOverlay = model.BeforeChannels.HasFlag(NotificationChannel.Overlay);
            StartToast = model.StartChannels.HasFlag(NotificationChannel.WindowsToast);
            StartOverlay = model.StartChannels.HasFlag(NotificationChannel.Overlay);
            StartAudio = model.StartChannels.HasFlag(NotificationChannel.Audio);

            StartFajr = model.StartEnabledPrayers.Contains(PrayerEvent.Fajr);
            StartDhuhr = model.StartEnabledPrayers.Contains(PrayerEvent.Dhuhr);
            StartAsr = model.StartEnabledPrayers.Contains(PrayerEvent.Asr);
            StartMaghrib = model.StartEnabledPrayers.Contains(PrayerEvent.Maghrib);
            StartIsha = model.StartEnabledPrayers.Contains(PrayerEvent.Isha);

            BeforeFajr = model.BeforeEnabledPrayers.Contains(PrayerEvent.Fajr);
            BeforeDhuhr = model.BeforeEnabledPrayers.Contains(PrayerEvent.Dhuhr);
            BeforeAsr = model.BeforeEnabledPrayers.Contains(PrayerEvent.Asr);
            BeforeMaghrib = model.BeforeEnabledPrayers.Contains(PrayerEvent.Maghrib);
            BeforeIsha = model.BeforeEnabledPrayers.Contains(PrayerEvent.Isha);

            FajrException = FormatException(model, PrayerEvent.Fajr);
            DhuhrException = FormatException(model, PrayerEvent.Dhuhr);
            AsrException = FormatException(model, PrayerEvent.Asr);
            MaghribException = FormatException(model, PrayerEvent.Maghrib);
            IshaException = FormatException(model, PrayerEvent.Isha);

            RebuildSoundOptions();
            SelectSounds(
                _appState.Settings.AdhanSoundId,
                _appState.Settings.PreAlertSoundId);

            RefreshLabels();
        }
        finally
        {
            _suppress = false;
        }
    }

    public void PreviewAdhan()
    {
        var id = SelectedAdhanSound?.Id ?? _appState.Settings.AdhanSoundId;
        _previewAudio.Play(SoundLibraryService.ProfileForSoundId(id));
    }

    public void PreviewPreAlert()
    {
        var id = SelectedPreAlertSound?.Id ?? _appState.Settings.PreAlertSoundId;
        _previewAudio.Play(SoundLibraryService.ProfileForSoundId(id));
    }

    public void StopPreview() => _previewAudio.Stop();

    public async Task AddUserSoundAsync(string filePath)
    {
        try
        {
            var entry = SoundLibraryService.ImportUserFile(filePath);
            var list = _appState.Settings.UserSounds.ToList();
            list.Add(entry);
            _suppress = true;
            try
            {
                await _appState.UpdateAsync(s => s.With(userSounds: list));
            }
            finally
            {
                _suppress = false;
            }

            RebuildSoundOptions();
            SelectedAdhanSound = AdhanSoundOptions.FirstOrDefault(o => o.Id == entry.Id)
                                 ?? SelectedAdhanSound;
            StatusMessage = _localization.Get("SoundAdded");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"AddUserSound failed: {ex.Message}");
            StatusMessage = _localization.Get("SoundAddFailed") + " " + ex.Message;
        }
    }

    private void RebuildSoundOptions()
    {
        var arabic = _localization.IsArabic;
        var catalog = SoundLibraryService.GetCatalog(_appState.Settings);

        AdhanSoundOptions.Clear();
        PreAlertSoundOptions.Clear();

        foreach (var item in catalog)
        {
            var pick = new SoundPickItem(item.Id, SoundLibraryService.DisplayName(item, arabic));
            // Full list available for both pickers (user may use a short adhan as pre-alert, etc.)
            AdhanSoundOptions.Add(pick);
            PreAlertSoundOptions.Add(new SoundPickItem(item.Id, pick.DisplayName));
        }

        SelectSounds(
            SelectedAdhanSound?.Id ?? _appState.Settings.AdhanSoundId,
            SelectedPreAlertSound?.Id ?? _appState.Settings.PreAlertSoundId);

        OnPropertyChanged(nameof(AdhanSoundOptions));
        OnPropertyChanged(nameof(PreAlertSoundOptions));
    }

    private void SelectSounds(string? adhanId, string? preId)
    {
        SelectedAdhanSound = AdhanSoundOptions.FirstOrDefault(o => o.Id == adhanId)
                             ?? AdhanSoundOptions.FirstOrDefault(o => o.Id == BuiltInSoundIds.AdhanAlaqsa)
                             ?? AdhanSoundOptions.FirstOrDefault();
        SelectedPreAlertSound = PreAlertSoundOptions.FirstOrDefault(o => o.Id == preId)
                                ?? PreAlertSoundOptions.FirstOrDefault(o => o.Id == BuiltInSoundIds.Takbir)
                                ?? PreAlertSoundOptions.FirstOrDefault();
    }

    public async Task SaveAsync()
    {
        try
        {
            if (!TryParseMinutes(GeneralBeforeMinutes, out var general, required: true))
            {
                StatusMessage = _localization.Get("InvalidOffsetMinutes");
                return;
            }

            var exceptions = new Dictionary<PrayerEvent, int?>();
            if (!TryCollectException(PrayerEvent.Fajr, FajrException, BeforeFajr, exceptions, out var err)
                || !TryCollectException(PrayerEvent.Dhuhr, DhuhrException, BeforeDhuhr, exceptions, out err)
                || !TryCollectException(PrayerEvent.Asr, AsrException, BeforeAsr, exceptions, out err)
                || !TryCollectException(PrayerEvent.Maghrib, MaghribException, BeforeMaghrib, exceptions, out err)
                || !TryCollectException(PrayerEvent.Isha, IshaException, BeforeIsha, exceptions, out err))
            {
                StatusMessage = err;
                return;
            }

            var beforeChannels = NotificationChannel.None;
            if (BeforeToast)
            {
                beforeChannels |= NotificationChannel.WindowsToast;
            }

            if (BeforeOverlay)
            {
                beforeChannels |= NotificationChannel.Overlay;
            }

            if (beforeChannels == NotificationChannel.None)
            {
                beforeChannels = NotificationChannel.WindowsToast;
            }

            var startChannels = NotificationChannel.None;
            if (StartToast)
            {
                startChannels |= NotificationChannel.WindowsToast;
            }

            if (StartOverlay)
            {
                startChannels |= NotificationChannel.Overlay;
            }

            if (StartAudio)
            {
                startChannels |= NotificationChannel.Audio;
            }

            if (startChannels == NotificationChannel.None)
            {
                startChannels = NotificationChannel.All;
            }

            var beforePrayers = new HashSet<PrayerEvent>();
            if (BeforeFajr)
            {
                beforePrayers.Add(PrayerEvent.Fajr);
            }

            if (BeforeDhuhr)
            {
                beforePrayers.Add(PrayerEvent.Dhuhr);
            }

            if (BeforeAsr)
            {
                beforePrayers.Add(PrayerEvent.Asr);
            }

            if (BeforeMaghrib)
            {
                beforePrayers.Add(PrayerEvent.Maghrib);
            }

            if (BeforeIsha)
            {
                beforePrayers.Add(PrayerEvent.Isha);
            }

            var startPrayers = new HashSet<PrayerEvent>();
            if (StartFajr)
            {
                startPrayers.Add(PrayerEvent.Fajr);
            }

            if (StartDhuhr)
            {
                startPrayers.Add(PrayerEvent.Dhuhr);
            }

            if (StartAsr)
            {
                startPrayers.Add(PrayerEvent.Asr);
            }

            if (StartMaghrib)
            {
                startPrayers.Add(PrayerEvent.Maghrib);
            }

            if (StartIsha)
            {
                startPrayers.Add(PrayerEvent.Isha);
            }

            var rules = NotificationRulesComposer.Compose(
                general,
                exceptions,
                beforePrayers,
                startPrayers,
                beforeChannels,
                startChannels,
                BeforeEnabled,
                StartEnabled);

            var adhanId = SelectedAdhanSound?.Id ?? BuiltInSoundIds.AdhanAlaqsa;
            var preId = SelectedPreAlertSound?.Id ?? BuiltInSoundIds.Takbir;
            var defaultAudio = SoundLibraryService.ProfileForSoundId(adhanId);

            _suppress = true;
            try
            {
                await _appState.UpdateAsync(s => s.With(
                    notificationRules: rules,
                    adhanSoundId: adhanId,
                    preAlertSoundId: preId,
                    defaultAudio: defaultAudio));
            }
            finally
            {
                _suppress = false;
            }

            StatusMessage = _localization.Get("AlertsSaved");
            LaunchLog.Write($"Alerts saved: general={general}m, exceptions={exceptions.Count}, adhan={adhanId}, pre={preId}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Alerts SaveAsync failed: {ex}");
            StatusMessage = "Save failed: " + ex.Message;
        }
    }

    private static string FormatException(NotificationRulesUiModel model, PrayerEvent prayer)
    {
        if (!model.BeforeExceptions.TryGetValue(prayer, out var minutes))
        {
            return string.Empty;
        }

        return minutes is > 0 ? LatinDigits.Number(minutes.Value) : string.Empty;
    }

    private bool TryCollectException(
        PrayerEvent prayer,
        string text,
        bool beforeEnabled,
        Dictionary<PrayerEvent, int?> exceptions,
        out string error)
    {
        error = string.Empty;
        var trimmed = LatinDigits.EnsureLatin(text).Trim();

        if (!beforeEnabled)
        {
            // Explicit cancel via disabled exception so general cannot re-include.
            exceptions[prayer] = null;
            return true;
        }

        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        if (!TryParseMinutes(trimmed, out var minutes, required: true) || minutes <= 0)
        {
            error = _localization.Get("InvalidOffsetMinutes");
            return false;
        }

        if (TryParseMinutes(GeneralBeforeMinutes, out var general, required: false)
            && minutes == general)
        {
            // Same as general — no exception row needed.
            return true;
        }

        exceptions[prayer] = minutes;
        return true;
    }

    private static bool TryParseMinutes(string text, out int minutes, bool required)
    {
        minutes = 0;
        var t = LatinDigits.EnsureLatin(text).Trim();
        if (string.IsNullOrEmpty(t))
        {
            return !required;
        }

        return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes)
               && minutes is >= 0 and <= 180;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SoundPickItem(string id, string displayName)
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;

    public override string ToString() => DisplayName;
}
