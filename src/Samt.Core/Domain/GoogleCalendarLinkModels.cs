namespace Samt.Core.Domain;

/// <summary>Persisted state of the optional Google Calendar link (no OAuth secrets).</summary>
public sealed class GoogleCalendarLinkState
{
    /// <summary>When true, sync may run (account connected and calendar known or first-connect pending).</summary>
    public bool IsLinked { get; init; }

    /// <summary>Google account email for display (not used for auth).</summary>
    public string? AccountEmail { get; init; }

    /// <summary>Google calendar id of the dedicated SAMT calendar.</summary>
    public string? CalendarId { get; init; }

    public DateTimeOffset? LastSyncUtc { get; init; }

    public bool LastSyncSucceeded { get; init; }

    /// <summary>User-visible summary of last sync (Latin digits applied by UI).</summary>
    public string LastSyncMessage { get; init; } = "";

    public int LastSkippedCount { get; init; }

    public GoogleCalendarLinkState With(
        bool? isLinked = null,
        string? accountEmail = null,
        bool replaceAccountEmail = false,
        string? calendarId = null,
        bool replaceCalendarId = false,
        DateTimeOffset? lastSyncUtc = null,
        bool replaceLastSyncUtc = false,
        bool? lastSyncSucceeded = null,
        string? lastSyncMessage = null,
        int? lastSkippedCount = null)
        => new()
        {
            IsLinked = isLinked ?? IsLinked,
            AccountEmail = replaceAccountEmail ? accountEmail : accountEmail ?? AccountEmail,
            CalendarId = replaceCalendarId ? calendarId : calendarId ?? CalendarId,
            LastSyncUtc = replaceLastSyncUtc ? lastSyncUtc : lastSyncUtc ?? LastSyncUtc,
            LastSyncSucceeded = lastSyncSucceeded ?? LastSyncSucceeded,
            LastSyncMessage = lastSyncMessage ?? LastSyncMessage,
            LastSkippedCount = lastSkippedCount ?? LastSkippedCount
        };
}

/// <summary>Delete tombstone so LWW does not resurrect a deleted reminder.</summary>
public sealed class CalendarSyncTombstone
{
    public Guid? ReminderId { get; init; }

    public string? GoogleEventId { get; init; }

    public DateTimeOffset DeletedUtc { get; init; }
}

/// <summary>
/// Snapshot of one Google Calendar event for pure reconciliation (App maps API → this).
/// </summary>
public sealed class GoogleCalendarEventSnapshot
{
    public required string EventId { get; init; }

    public string Title { get; init; } = "";

    public string Note { get; init; } = "";

    public DateOnly? CivilDate { get; init; }

    /// <summary>HH:mm in active location TZ when timed; null if all-day or unparsed.</summary>
    public string? Time { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; }

    public bool IsCancelled { get; init; }

    public bool IsAllDay { get; init; }

    public bool IsRecurring { get; init; }

    /// <summary>
    /// Private extended property <c>samtReminderId</c> when SAMT created/updated the event.
    /// Used to rematch after disconnect/reconnect without duplicating.
    /// </summary>
    public Guid? SamtReminderId { get; init; }

    /// <summary>True when the event can map 1:1 to a user calendar reminder.</summary>
    public bool IsImportable
        => !IsCancelled
           && !IsAllDay
           && !IsRecurring
           && CivilDate is not null
           && !string.IsNullOrWhiteSpace(Time)
           && !string.IsNullOrWhiteSpace(Title);
}

/// <summary>Remote mutation the App host must execute after planning.</summary>
public enum GoogleRemoteActionKind
{
    Create,
    Update,
    Delete
}

public sealed class GoogleRemoteAction
{
    public required GoogleRemoteActionKind Kind { get; init; }

    public Guid ReminderId { get; init; }

    public string? GoogleEventId { get; init; }

    public UserCalendarReminder? Reminder { get; init; }
}

/// <summary>Result of pure bi-di reconcile for user calendar reminders.</summary>
public sealed class GoogleCalendarSyncPlan
{
    public required IReadOnlyList<UserCalendarReminder> Reminders { get; init; }

    public required IReadOnlyList<CalendarSyncTombstone> Tombstones { get; init; }

    public required IReadOnlyList<GoogleRemoteAction> RemoteActions { get; init; }

    public required IReadOnlyList<string> SkipReasons { get; init; }

    public int ImportedCount { get; init; }

    public int UpdatedLocalCount { get; init; }

    public int DeletedLocalCount { get; init; }
}
