using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Samt.Core.Calendar;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt.Core.Time;
using Samt_App.Services;

namespace Samt_App.ViewModels;

public sealed class CalendarDayVm
{
    public bool IsPlaceholder { get; init; }

    public string HijriDayText { get; init; } = "";

    public string GregorianText { get; init; } = "";

    public DateOnly CivilDate { get; init; }

    public HijriDate Hijri { get; init; }

    public ResolvedSpecialDay? SpecialDay { get; init; }

    public SpecialDayMark Mark { get; init; }

    public bool IsToday { get; init; }

    public bool IsRamadan { get; init; }

    public string SpecialLabel { get; init; } = "";

    public bool ShowIslamicDot => Mark is SpecialDayMark.Islamic or SpecialDayMark.Both;

    public bool ShowCountryDot => Mark is SpecialDayMark.Country or SpecialDayMark.Both;

    public bool HasContent => !IsPlaceholder;
}

public sealed class CalendarViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly LocalizationService _localization;
    private int _hijriYear;
    private int _hijriMonth;
    private string _monthTitle = "";
    private string _subtitle = "";

    public CalendarViewModel(AppState appState, LocalizationService localization)
    {
        _appState = appState;
        _localization = localization;
        JumpToToday();
        _appState.SettingsChanged += (_, _) => Refresh();
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

    public int HijriYear => _hijriYear;

    public int HijriMonth => _hijriMonth;

    public void JumpToToday()
    {
        var offset = HijriConverter.ClampDayOffset(_appState.Settings.HijriDayOffset);
        var today = TodayCivil();
        var hijri = HijriConverter.FromGregorian(today, offset);
        _hijriYear = hijri.Year;
        _hijriMonth = hijri.Month;
        Refresh();
    }

    public void PrevMonth()
    {
        _hijriMonth--;
        if (_hijriMonth < 1)
        {
            _hijriMonth = 12;
            _hijriYear--;
        }

        Refresh();
    }

    public void NextMonth()
    {
        _hijriMonth++;
        if (_hijriMonth > 12)
        {
            _hijriMonth = 1;
            _hijriYear++;
        }

        Refresh();
    }

    public void Refresh()
    {
        var settings = _appState.Settings;
        var offset = HijriConverter.ClampDayOffset(settings.HijriDayOffset);
        var location = settings.GetActiveLocation();
        var country = CalendarCountryResolver.Resolve(
            settings.CalendarCountryOverride,
            location?.CountryCode);

        var cells = SpecialDayResolver.ForHijriMonth(_hijriYear, _hijriMonth, offset, country);
        var today = TodayCivil();

        var monthName = _localization.Get($"Hijri.Month.{_hijriMonth}");
        MonthTitle = $"{monthName} {LatinDigits.Number(_hijriYear)}";
        Subtitle = LatinDigits.EnsureLatin(country)
                   + (location is null ? "" : " · " + location.DisplayName);

        WeekdayHeaders.Clear();
        for (var i = 0; i < 7; i++)
        {
            WeekdayHeaders.Add(_localization.Get($"Weekday.Short.{i}"));
        }

        Days.Clear();
        if (cells.Count == 0)
        {
            return;
        }

        var lead = (int)cells[0].CivilDate.DayOfWeek;
        for (var i = 0; i < lead; i++)
        {
            Days.Add(new CalendarDayVm { IsPlaceholder = true });
        }

        foreach (var cell in cells)
        {
            var specialLabel = cell.SpecialDay is { } special
                ? _localization.Get(special.PrimaryDisplayKey)
                : "";

            Days.Add(new CalendarDayVm
            {
                IsPlaceholder = false,
                HijriDayText = LatinDigits.Number(cell.Hijri.Day),
                GregorianText = LatinDigits.Date(cell.CivilDate, "d MMM"),
                CivilDate = cell.CivilDate,
                Hijri = cell.Hijri,
                SpecialDay = cell.SpecialDay,
                Mark = cell.Mark,
                IsToday = cell.CivilDate == today,
                IsRamadan = cell.IsRamadan,
                SpecialLabel = specialLabel
            });
        }

        OnPropertyChanged(nameof(HijriYear));
        OnPropertyChanged(nameof(HijriMonth));
        // Always notify so the page rebuilds the grid when settings (offset, country) change.
        OnPropertyChanged(nameof(Days));
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
