using System.Globalization;
using Samt.Core.Domain;
using Samt.Core.Time;

namespace Samt.Core.Calendar;

/// <summary>One toast-only special-day fire opportunity for the App host.</summary>
public sealed class PlannedSpecialDayReminder
{
    /// <summary>Stable id: <c>special:{yyyy-MM-dd}:{primaryDefId}</c>.</summary>
    public required string Id { get; init; }

    public required DateTimeOffset FireAt { get; init; }

    public required DateOnly CivilDate { get; init; }

    public required string PrimaryDisplayKey { get; init; }

    public required IReadOnlyList<string> DefinitionIds { get; init; }

    public required IReadOnlyList<string> DisplayKeys { get; init; }

    public required SpecialDaySources Sources { get; init; }
}

/// <summary>
/// Sibling planner for special-day morning toasts (ADR-0002). Pure Core — no UI, no Windows APIs.
/// Does not use <see cref="Domain.NotificationRule"/> or adhan channels.
/// </summary>
public static class SpecialDayReminderPlanner
{
    /// <summary>Future special-day fire after <paramref name="now"/> for the civil day of <paramref name="now"/>.</summary>
    public static IReadOnlyList<PlannedSpecialDayReminder> Plan(
        DateTimeOffset now,
        AppSettings settings,
        TimeZoneInfo locationTimeZone,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(locationTimeZone);

        return BuildEligible(now, settings, locationTimeZone, countryCode)
            .Where(r => r.FireAt > now)
            .OrderBy(r => r.FireAt)
            .ToList();
    }

    /// <summary>
    /// Special-day fires that should already have happened in <c>(since, now − grace]</c>.
    /// Used for resume / late-start summary toasts (no late audio).
    /// </summary>
    public static IReadOnlyList<PlannedSpecialDayReminder> PlanMissed(
        DateTimeOffset now,
        DateTimeOffset since,
        AppSettings settings,
        TimeZoneInfo locationTimeZone,
        TimeSpan? lateGrace = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(locationTimeZone);

        var grace = lateGrace ?? TimeSpan.FromMinutes(2);
        var upper = now - grace;
        if (upper <= since)
        {
            return [];
        }

        return BuildEligible(now, settings, locationTimeZone, countryCode)
            .Where(r => r.FireAt > since && r.FireAt <= upper)
            .OrderBy(r => r.FireAt)
            .ToList();
    }

    private static IReadOnlyList<PlannedSpecialDayReminder> BuildEligible(
        DateTimeOffset now,
        AppSettings settings,
        TimeZoneInfo locationTimeZone,
        string? countryCode)
    {
        if (!settings.SpecialDayRemindersEnabled)
        {
            return [];
        }

        if (!settings.SpecialDayIslamicSetEnabled && !settings.SpecialDayCountrySetEnabled)
        {
            return [];
        }

        var localNow = TimeZoneInfo.ConvertTime(now, locationTimeZone);
        var civilToday = DateOnly.FromDateTime(localNow.DateTime);
        var offset = HijriConverter.ClampDayOffset(settings.HijriDayOffset);
        var effectiveCountry = countryCode
            ?? CalendarCountryResolver.Resolve(
                settings.CalendarCountryOverride,
                settings.GetActiveLocation()?.CountryCode);

        var day = SpecialDayResolver.ForCivilDate(civilToday, offset, effectiveCountry);
        if (day is null)
        {
            return [];
        }

        var muted = new HashSet<string>(
            settings.SpecialDayMutedIds ?? [],
            StringComparer.OrdinalIgnoreCase);

        var contributing = day.Definitions
            .Where(d => !muted.Contains(d.Id))
            .Where(d => d.Source switch
            {
                SpecialDaySource.Islamic => settings.SpecialDayIslamicSetEnabled,
                SpecialDaySource.Country => settings.SpecialDayCountrySetEnabled,
                _ => false
            })
            .ToList();

        if (contributing.Count == 0)
        {
            return [];
        }

        if (!TryParseReminderTime(settings.SpecialDayReminderTime, out var clock))
        {
            clock = new TimeOnly(9, 0);
        }

        var fireLocal = civilToday.ToDateTime(clock, DateTimeKind.Unspecified);
        var fireAt = new DateTimeOffset(fireLocal, locationTimeZone.GetUtcOffset(fireLocal));

        var sources = SpecialDaySources.None;
        foreach (var def in contributing)
        {
            sources |= def.Source == SpecialDaySource.Islamic
                ? SpecialDaySources.Islamic
                : SpecialDaySources.Country;
        }

        var primary = contributing[0];
        var ids = contributing.Select(d => d.Id).ToList();
        var keys = contributing.Select(d => d.DisplayKey).ToList();
        var civilKey = civilToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return
        [
            new PlannedSpecialDayReminder
            {
                Id = $"special:{civilKey}:{primary.Id}",
                FireAt = fireAt,
                CivilDate = civilToday,
                PrimaryDisplayKey = primary.DisplayKey,
                DefinitionIds = ids,
                DisplayKeys = keys,
                Sources = sources
            }
        ];
    }

    private static bool TryParseReminderTime(string? value, out TimeOnly clock)
    {
        clock = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, out clock)
               || TimeOnly.TryParse(trimmed, out clock);
    }
}
