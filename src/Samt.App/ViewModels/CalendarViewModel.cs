using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Samt.Core.Calendar;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Time;
using Samt_App.Services;

namespace Samt_App.ViewModels;

// App static accessors live in Samt_App.App

public sealed class CalendarDayVm
{
    public bool IsPlaceholder { get; init; }

    /// <summary>Primary day number shown large (Hijri day or Gregorian day depending on mode).</summary>
    public string PrimaryDayText { get; init; } = "";

    /// <summary>Secondary dual date line.</summary>
    public string SecondaryText { get; init; } = "";

    public DateOnly CivilDate { get; init; }

    public HijriDate Hijri { get; init; }

    public ResolvedSpecialDay? SpecialDay { get; init; }

    public SpecialDayMark Mark { get; init; }

    public bool IsToday { get; init; }

    public bool IsRamadan { get; init; }

    public string SpecialLabel { get; init; } = "";

    public int UserReminderCount { get; init; }

    public bool ShowIslamicDot => Mark is SpecialDayMark.Islamic or SpecialDayMark.Both;

    public bool ShowCountryDot => Mark is SpecialDayMark.Country or SpecialDayMark.Both;

    public bool ShowUserDot => UserReminderCount > 0;
}

public sealed class CalendarViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly LocalizationService _localization;
    private int _viewYear;
    private int _viewMonth;
    private string _monthTitle = "";
    private string _subtitle = "";
    private CalendarPrimaryMode _mode = CalendarPrimaryMode.Hijri;

    public CalendarViewModel(AppState appState, LocalizationService localization)
    {
        _appState = appState;
        _localization = localization;
        _mode = appState.Settings.CalendarPrimaryMode;
        JumpToToday();
        _appState.SettingsChanged += (_, _) =>
        {
            _mode = _appState.Settings.CalendarPrimaryMode;
            Refresh();
        };
        _localization.LanguageChanged += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalendarDayVm> Days { get; } = [];

    public ObservableCollection<string> WeekdayHeaders { get; } = [];

    public string MonthTitle
    {
        get => _monthTitle;
        private set
        {
            if (_monthTitle == value)
            {
                return;
            }

            _monthTitle = value;
            OnPropertyChanged();
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        private set
        {
            if (_subtitle == value)
            {
                return;
            }

            _subtitle = value;
            OnPropertyChanged();
        }
    }

    public CalendarPrimaryMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHijriMode));
            OnPropertyChanged(nameof(ModeToggleLabel));
        }
    }

    public bool IsHijriMode => Mode == CalendarPrimaryMode.Hijri;

    public string ModeToggleLabel
        => IsHijriMode
            ? _localization.Get("CalendarShowGregorian")
            : _localization.Get("CalendarShowHijri");

    public async Task ToggleModeAsync()
    {
        var next = Mode == CalendarPrimaryMode.Hijri
            ? CalendarPrimaryMode.Gregorian
            : CalendarPrimaryMode.Hijri;
        await _appState.UpdateAsync(s => s.With(calendarPrimaryMode: next));
        Mode = next;
        JumpToToday();
    }

    public void JumpToToday()
    {
        var today = TodayCivil();
        if (Mode == CalendarPrimaryMode.Hijri)
        {
            var offset = HijriConverter.ClampDayOffset(_appState.Settings.HijriDayOffset);
            var hijri = HijriConverter.FromGregorian(today, offset);
            _viewYear = hijri.Year;
            _viewMonth = hijri.Month;
        }
        else
        {
            _viewYear = today.Year;
            _viewMonth = today.Month;
        }

        Refresh();
    }

    public void PrevMonth()
    {
        _viewMonth--;
        if (_viewMonth < 1)
        {
            _viewMonth = 12;
            _viewYear--;
        }

        Refresh();
    }

    public void NextMonth()
    {
        _viewMonth++;
        if (_viewMonth > 12)
        {
            _viewMonth = 1;
            _viewYear++;
        }

        Refresh();
    }

    public void Refresh()
    {
        var settings = _appState.Settings;
        Mode = settings.CalendarPrimaryMode;
        var offset = HijriConverter.ClampDayOffset(settings.HijriDayOffset);
        var location = settings.GetActiveLocation();
        var country = CalendarCountryResolver.Resolve(
            settings.CalendarCountryOverride,
            location?.CountryCode);
        var today = TodayCivil();

        WeekdayHeaders.Clear();
        for (var i = 0; i < 7; i++)
        {
            WeekdayHeaders.Add(_localization.Get($"Weekday.Short.{i}"));
        }

        Days.Clear();

        if (Mode == CalendarPrimaryMode.Hijri)
        {
            BuildHijriMonth(offset, country, today, location);
        }
        else
        {
            BuildGregorianMonth(offset, country, today, location);
        }

        OnPropertyChanged(nameof(ModeToggleLabel));
        OnPropertyChanged(nameof(Days));
    }

    private void BuildHijriMonth(
        int offset,
        string country,
        DateOnly today,
        LocationProfile? location)
    {
        var cells = SpecialDayResolver.ForHijriMonth(_viewYear, _viewMonth, offset, country);
        var monthName = _localization.Get($"Hijri.Month.{_viewMonth}");
        MonthTitle = $"{monthName} {LatinDigits.Number(_viewYear)}";
        Subtitle = FormatSubtitle(country, location);

        if (cells.Count == 0)
        {
            return;
        }

        var lead = (int)cells[0].CivilDate.DayOfWeek;
        AddPlaceholders(lead);

        foreach (var cell in cells)
        {
            Days.Add(ToDayVm(
                cell.CivilDate,
                cell.Hijri,
                cell.SpecialDay,
                primaryDay: cell.Hijri.Day,
                secondary: LatinDigits.Date(cell.CivilDate, "d MMM"),
                today));
        }
    }

    private void BuildGregorianMonth(
        int offset,
        string country,
        DateOnly today,
        LocationProfile? location)
    {
        var daysInMonth = DateTime.DaysInMonth(_viewYear, _viewMonth);
        var first = new DateOnly(_viewYear, _viewMonth, 1);
        var culture = CultureInfo.GetCultureInfo("en-US");
        var monthName = first.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture);
        MonthTitle = $"{monthName} {LatinDigits.Number(_viewYear)}";
        Subtitle = FormatSubtitle(country, location);

        AddPlaceholders((int)first.DayOfWeek);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var civil = new DateOnly(_viewYear, _viewMonth, day);
            var hijri = HijriConverter.FromGregorian(civil, offset);
            var special = SpecialDayResolver.ForCivilDate(civil, offset, country);
            var hijriMonth = _localization.Get($"Hijri.Month.{hijri.Month}");
            var secondary = $"{LatinDigits.Number(hijri.Day)} {hijriMonth}";
            Days.Add(ToDayVm(civil, hijri, special, primaryDay: day, secondary, today));
        }
    }

    private CalendarDayVm ToDayVm(
        DateOnly civil,
        HijriDate hijri,
        ResolvedSpecialDay? special,
        int primaryDay,
        string secondary,
        DateOnly today)
    {
        var specialLabel = special is null ? "" : _localization.Get(special.PrimaryDisplayKey);
        var userCount = _appState.Settings.UserCalendarReminders
            .Count(r => r.Enabled && r.CivilDate == civil);

        return new CalendarDayVm
        {
            IsPlaceholder = false,
            PrimaryDayText = LatinDigits.Number(primaryDay),
            SecondaryText = secondary,
            CivilDate = civil,
            Hijri = hijri,
            SpecialDay = special,
            Mark = special?.Mark ?? SpecialDayMark.None,
            IsToday = civil == today,
            IsRamadan = hijri.IsRamadan,
            SpecialLabel = specialLabel,
            UserReminderCount = userCount
        };
    }

    private string FormatSubtitle(string country, LocationProfile? location)
        => LatinDigits.EnsureLatin(country)
           + (location is null ? "" : " · " + location.DisplayName);

    private void AddPlaceholders(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Days.Add(new CalendarDayVm { IsPlaceholder = true });
        }
    }

    public bool IsDefinitionMuted(string definitionId)
    {
        return _appState.Settings.SpecialDayMutedIds.Any(id =>
            string.Equals(id, definitionId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetDayMutedAsync(ResolvedSpecialDay day, bool muted)
    {
        var current = _appState.Settings.SpecialDayMutedIds.ToList();
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (muted)
        {
            foreach (var id in day.DefinitionIds)
            {
                if (!current.Any(x => comparer.Equals(x, id)))
                {
                    current.Add(id);
                }
            }
        }
        else
        {
            current.RemoveAll(id => day.DefinitionIds.Any(d => comparer.Equals(d, id)));
        }

        await _appState.UpdateAsync(s => s.With(specialDayMutedIds: current));
        Refresh();
    }

    public IReadOnlyList<UserCalendarReminder> RemindersForDay(DateOnly civil)
        => _appState.Settings.UserCalendarReminders
            .Where(r => r.CivilDate == civil)
            .OrderBy(r => r.Time, StringComparer.Ordinal)
            .ToList();

    public async Task AddUserReminderAsync(
        DateOnly civil,
        string title,
        string note,
        string time,
        int repeatCount,
        int intervalMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var list = _appState.Settings.UserCalendarReminders.ToList();
        list.Add(new UserCalendarReminder
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Note = note?.Trim() ?? "",
            CivilDate = civil,
            Time = time,
            RepeatCount = Math.Clamp(repeatCount, 1, 20),
            IntervalMinutes = Math.Clamp(intervalMinutes, 0, 1440),
            Enabled = true,
            LocalUpdatedUtc = now
        });
        await _appState.UpdateAsync(s => s.With(userCalendarReminders: list));
        Samt_App.App.GoogleCalendar?.NotifyLocalReminderChanged();
        Refresh();
    }

    public async Task UpdateUserReminderAsync(
        Guid id,
        string title,
        string note,
        string time,
        int repeatCount,
        int intervalMinutes,
        bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var list = _appState.Settings.UserCalendarReminders.ToList();
        var idx = list.FindIndex(r => r.Id == id);
        if (idx < 0)
        {
            return;
        }

        var existing = list[idx];
        list[idx] = GoogleCalendarSyncPlanner.TouchLocal(
            new UserCalendarReminder
            {
                Id = existing.Id,
                Title = title.Trim(),
                Note = note?.Trim() ?? "",
                CivilDate = existing.CivilDate,
                Time = time,
                RepeatCount = Math.Clamp(repeatCount, 1, 20),
                IntervalMinutes = Math.Clamp(intervalMinutes, 0, 1440),
                Enabled = enabled,
                GoogleEventId = existing.GoogleEventId,
                LocalUpdatedUtc = existing.LocalUpdatedUtc,
                LastSyncedUtc = existing.LastSyncedUtc
            },
            now);

        await _appState.UpdateAsync(s => s.With(userCalendarReminders: list));
        Samt_App.App.GoogleCalendar?.NotifyLocalReminderChanged();
        Refresh();
    }

    public async Task DeleteUserReminderAsync(Guid id)
    {
        var existing = _appState.Settings.UserCalendarReminders.FirstOrDefault(r => r.Id == id);
        var list = _appState.Settings.UserCalendarReminders.Where(r => r.Id != id).ToList();
        var stones = existing is null
            ? _appState.Settings.CalendarSyncTombstones
            : GoogleCalendarSyncPlanner.TombstoneDelete(
                _appState.Settings.CalendarSyncTombstones,
                existing.Id,
                existing.GoogleEventId,
                DateTimeOffset.UtcNow);

        await _appState.UpdateAsync(s => s.With(
            userCalendarReminders: list,
            calendarSyncTombstones: stones));
        Samt_App.App.GoogleCalendar?.NotifyLocalReminderChanged();
        Refresh();
    }

    private DateOnly TodayCivil()
    {
        try
        {
            var location = _appState.RequireActiveLocation();
            var tz = KnownLocations.ResolveTimeZone(location.TimeZoneId);
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz);
            return DateOnly.FromDateTime(local.DateTime);
        }
        catch
        {
            return DateOnly.FromDateTime(DateTime.Now);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
