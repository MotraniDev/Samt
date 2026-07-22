namespace Samt.Core.Domain;

/// <summary>Which calendar system is primary on the Calendar page grid.</summary>
public enum CalendarPrimaryMode
{
    Hijri = 0,
    Gregorian = 1
}

/// <summary>
/// How calendar / user day reminders are delivered (not prayer adhan channels).
/// Flags may be combined.
/// </summary>
[Flags]
public enum CalendarReminderDelivery
{
    None = 0,
    /// <summary>Windows toast / balloon.</summary>
    WindowsNotification = 1,
    /// <summary>Play a short cue (pre-alert library sound).</summary>
    Sound = 2,
    /// <summary>In-app silent floating window with title/note (no adhan).</summary>
    SilentWindow = 4
}

/// <summary>
/// User-authored one-shot or repeating reminder on a civil calendar day
/// (title + note, first local time, optional repeats with interval).
/// </summary>
public sealed class UserCalendarReminder
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string Note { get; init; } = "";

    /// <summary>Civil Gregorian date the reminder is anchored to.</summary>
    public required DateOnly CivilDate { get; init; }

    /// <summary>First fire local clock time (HH:mm, Latin digits).</summary>
    public string Time { get; init; } = "09:00";

    /// <summary>Total fires including the first (1–20).</summary>
    public int RepeatCount { get; init; } = 1;

    /// <summary>Minutes between successive fires when <see cref="RepeatCount"/> &gt; 1 (1–1440).</summary>
    public int IntervalMinutes { get; init; } = 5;

    public bool Enabled { get; init; } = true;

    /// <summary>Linked Google event id on the SAMT Google calendar; null if never pushed.</summary>
    public string? GoogleEventId { get; init; }

    /// <summary>When the user last changed title/note/date/time (or import applied). Used for LWW.</summary>
    public DateTimeOffset LocalUpdatedUtc { get; init; }

    /// <summary>When local content last matched Google after a successful sync.</summary>
    public DateTimeOffset? LastSyncedUtc { get; init; }
}
