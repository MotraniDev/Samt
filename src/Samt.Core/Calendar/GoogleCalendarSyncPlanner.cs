using Samt.Core.Domain;

namespace Samt.Core.Calendar;

/// <summary>
/// Pure bi-di planner for user calendar reminders ↔ dedicated SAMT Google calendar.
/// No network. App executes <see cref="GoogleCalendarSyncPlan.RemoteActions"/> then patches event ids.
/// </summary>
public static class GoogleCalendarSyncPlanner
{
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// Plans local mutations and remote actions from current local state + remote snapshots.
    /// Whole-event last-write-wins by timestamp; deletes use tombstones.
    /// </summary>
    public static GoogleCalendarSyncPlan Plan(
        IReadOnlyList<UserCalendarReminder> localReminders,
        IReadOnlyList<CalendarSyncTombstone> tombstones,
        IReadOnlyList<GoogleCalendarEventSnapshot> remoteEvents,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(localReminders);
        ArgumentNullException.ThrowIfNull(tombstones);
        ArgumentNullException.ThrowIfNull(remoteEvents);

        var reminders = localReminders.ToList();
        var stones = tombstones
            .Where(t => nowUtc - t.DeletedUtc <= TombstoneRetention)
            .ToList();

        var skipReasons = new List<string>();
        var remoteActions = new List<GoogleRemoteAction>();
        var imported = 0;
        var updatedLocal = 0;
        var deletedLocal = 0;

        var byEventId = reminders
            .Where(r => !string.IsNullOrWhiteSpace(r.GoogleEventId))
            .GroupBy(r => r.GoogleEventId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var tombstoneEventIds = new HashSet<string>(
            stones.Where(t => !string.IsNullOrWhiteSpace(t.GoogleEventId)).Select(t => t.GoogleEventId!),
            StringComparer.Ordinal);
        var tombstoneReminderIds = new HashSet<Guid>(
            stones.Where(t => t.ReminderId is { } id && id != Guid.Empty).Select(t => t.ReminderId!.Value));

        var remoteById = remoteEvents
            .GroupBy(e => e.EventId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Rematch after disconnect: local lost GoogleEventId but remote still has samtReminderId.
        for (var i = 0; i < reminders.Count; i++)
        {
            var local = reminders[i];
            if (!string.IsNullOrWhiteSpace(local.GoogleEventId) || tombstoneReminderIds.Contains(local.Id))
            {
                continue;
            }

            var match = remoteEvents.FirstOrDefault(e =>
                !e.IsCancelled
                && e.SamtReminderId == local.Id
                && !tombstoneEventIds.Contains(e.EventId)
                && !byEventId.ContainsKey(e.EventId));
            if (match is null)
            {
                continue;
            }

            var rematched = RelinkEventId(local, match.EventId);
            reminders[i] = rematched;
            byEventId[match.EventId] = rematched;
        }

        // --- Remote → local ---
        foreach (var remote in remoteEvents)
        {
            if (remote.IsCancelled)
            {
                if (byEventId.TryGetValue(remote.EventId, out var linkedCancel))
                {
                    reminders.RemoveAll(r => r.Id == linkedCancel.Id);
                    byEventId.Remove(remote.EventId);
                    deletedLocal++;
                    AddTombstone(stones, linkedCancel.Id, remote.EventId, nowUtc);
                }

                continue;
            }

            if (!remote.IsImportable)
            {
                if (remote.IsAllDay)
                {
                    skipReasons.Add($"all-day:{remote.EventId}");
                }
                else if (remote.IsRecurring)
                {
                    skipReasons.Add($"recurring:{remote.EventId}");
                }
                else if (string.IsNullOrWhiteSpace(remote.Title))
                {
                    skipReasons.Add($"no-title:{remote.EventId}");
                }
                else
                {
                    skipReasons.Add($"unmappable:{remote.EventId}");
                }

                continue;
            }

            if (tombstoneEventIds.Contains(remote.EventId))
            {
                // Local delete wins: ensure remote is deleted.
                remoteActions.Add(new GoogleRemoteAction
                {
                    Kind = GoogleRemoteActionKind.Delete,
                    ReminderId = Guid.Empty,
                    GoogleEventId = remote.EventId
                });
                continue;
            }

            if (byEventId.TryGetValue(remote.EventId, out var local))
            {
                if (tombstoneReminderIds.Contains(local.Id))
                {
                    remoteActions.Add(new GoogleRemoteAction
                    {
                        Kind = GoogleRemoteActionKind.Delete,
                        ReminderId = local.Id,
                        GoogleEventId = remote.EventId
                    });
                    continue;
                }

                // Whole-event LWW
                if (remote.UpdatedUtc > local.LocalUpdatedUtc)
                {
                    var idx = reminders.FindIndex(r => r.Id == local.Id);
                    if (idx >= 0)
                    {
                        reminders[idx] = ApplyRemoteToLocal(local, remote);
                        byEventId[remote.EventId] = reminders[idx];
                        updatedLocal++;
                    }
                }
                else if (local.LocalUpdatedUtc > remote.UpdatedUtc
                         || local.LastSyncedUtc is null
                         || local.LocalUpdatedUtc > local.LastSyncedUtc)
                {
                    // Local newer or dirty → push
                    remoteActions.Add(new GoogleRemoteAction
                    {
                        Kind = GoogleRemoteActionKind.Update,
                        ReminderId = local.Id,
                        GoogleEventId = remote.EventId,
                        Reminder = local
                    });
                }

                continue;
            }

            // Avoid importing a second copy when remote still carries our reminder id.
            if (remote.SamtReminderId is { } samtId
                && samtId != Guid.Empty
                && reminders.Any(r => r.Id == samtId))
            {
                skipReasons.Add($"already-local:{remote.EventId}");
                continue;
            }

            // New remote event → import
            var created = new UserCalendarReminder
            {
                Id = remote.SamtReminderId is { } keepId && keepId != Guid.Empty
                    ? keepId
                    : Guid.NewGuid(),
                Title = remote.Title.Trim(),
                Note = remote.Note?.Trim() ?? "",
                CivilDate = remote.CivilDate!.Value,
                Time = remote.Time!,
                RepeatCount = 1,
                IntervalMinutes = 5,
                Enabled = true,
                GoogleEventId = remote.EventId,
                LocalUpdatedUtc = remote.UpdatedUtc,
                LastSyncedUtc = nowUtc
            };
            reminders.Add(created);
            byEventId[remote.EventId] = created;
            imported++;
        }

        // --- Local → remote (creates + deletes when remote gone) ---
        foreach (var local in reminders.ToList())
        {
            if (tombstoneReminderIds.Contains(local.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(local.GoogleEventId))
            {
                remoteActions.Add(new GoogleRemoteAction
                {
                    Kind = GoogleRemoteActionKind.Create,
                    ReminderId = local.Id,
                    Reminder = local
                });
                continue;
            }

            if (!remoteById.ContainsKey(local.GoogleEventId))
            {
                // Remote missing and not a pending create — treat as remote delete (unless we still need to create after first connect race).
                // If never synced, try create instead of delete.
                if (local.LastSyncedUtc is null)
                {
                    remoteActions.Add(new GoogleRemoteAction
                    {
                        Kind = GoogleRemoteActionKind.Create,
                        ReminderId = local.Id,
                        Reminder = local
                    });
                }
                else
                {
                    reminders.RemoveAll(r => r.Id == local.Id);
                    byEventId.Remove(local.GoogleEventId);
                    deletedLocal++;
                    AddTombstone(stones, local.Id, local.GoogleEventId, nowUtc);
                }
            }
        }

        // Deduplicate remote actions (prefer Delete > Update > Create per event/reminder)
        remoteActions = DeduplicateActions(remoteActions);

        return new GoogleCalendarSyncPlan
        {
            Reminders = reminders,
            Tombstones = stones,
            RemoteActions = remoteActions,
            SkipReasons = skipReasons,
            ImportedCount = imported,
            UpdatedLocalCount = updatedLocal,
            DeletedLocalCount = deletedLocal
        };
    }

    /// <summary>Record a user delete for bi-di (local already removed).</summary>
    public static IReadOnlyList<CalendarSyncTombstone> TombstoneDelete(
        IReadOnlyList<CalendarSyncTombstone> existing,
        Guid reminderId,
        string? googleEventId,
        DateTimeOffset nowUtc)
    {
        var list = existing.ToList();
        AddTombstone(list, reminderId, googleEventId, nowUtc);
        return list;
    }

    public static UserCalendarReminder TouchLocal(UserCalendarReminder reminder, DateTimeOffset nowUtc)
        => new()
        {
            Id = reminder.Id,
            Title = reminder.Title,
            Note = reminder.Note,
            CivilDate = reminder.CivilDate,
            Time = reminder.Time,
            RepeatCount = reminder.RepeatCount,
            IntervalMinutes = reminder.IntervalMinutes,
            Enabled = reminder.Enabled,
            GoogleEventId = reminder.GoogleEventId,
            LocalUpdatedUtc = nowUtc,
            LastSyncedUtc = reminder.LastSyncedUtc
        };

    public static UserCalendarReminder WithGoogleLink(
        UserCalendarReminder reminder,
        string googleEventId,
        DateTimeOffset syncedUtc)
        => new()
        {
            Id = reminder.Id,
            Title = reminder.Title,
            Note = reminder.Note,
            CivilDate = reminder.CivilDate,
            Time = reminder.Time,
            RepeatCount = reminder.RepeatCount,
            IntervalMinutes = reminder.IntervalMinutes,
            Enabled = reminder.Enabled,
            GoogleEventId = googleEventId,
            LocalUpdatedUtc = reminder.LocalUpdatedUtc,
            LastSyncedUtc = syncedUtc
        };

    private static UserCalendarReminder RelinkEventId(UserCalendarReminder reminder, string googleEventId)
        => new()
        {
            Id = reminder.Id,
            Title = reminder.Title,
            Note = reminder.Note,
            CivilDate = reminder.CivilDate,
            Time = reminder.Time,
            RepeatCount = reminder.RepeatCount,
            IntervalMinutes = reminder.IntervalMinutes,
            Enabled = reminder.Enabled,
            GoogleEventId = googleEventId,
            LocalUpdatedUtc = reminder.LocalUpdatedUtc,
            LastSyncedUtc = reminder.LastSyncedUtc
        };

    private static UserCalendarReminder ApplyRemoteToLocal(
        UserCalendarReminder local,
        GoogleCalendarEventSnapshot remote)
        => new()
        {
            Id = local.Id,
            Title = remote.Title.Trim(),
            Note = remote.Note?.Trim() ?? "",
            CivilDate = remote.CivilDate!.Value,
            Time = remote.Time!,
            // Keep local delivery prefs
            RepeatCount = local.RepeatCount,
            IntervalMinutes = local.IntervalMinutes,
            Enabled = local.Enabled,
            GoogleEventId = remote.EventId,
            LocalUpdatedUtc = remote.UpdatedUtc,
            LastSyncedUtc = remote.UpdatedUtc
        };

    private static void AddTombstone(
        List<CalendarSyncTombstone> stones,
        Guid? reminderId,
        string? googleEventId,
        DateTimeOffset deletedUtc)
    {
        if ((reminderId is null || reminderId == Guid.Empty) && string.IsNullOrWhiteSpace(googleEventId))
        {
            return;
        }

        // Refresh existing
        stones.RemoveAll(t =>
            (reminderId is { } rid && rid != Guid.Empty && t.ReminderId == rid)
            || (!string.IsNullOrWhiteSpace(googleEventId)
                && string.Equals(t.GoogleEventId, googleEventId, StringComparison.Ordinal)));

        stones.Add(new CalendarSyncTombstone
        {
            ReminderId = reminderId is { } id && id != Guid.Empty ? id : null,
            GoogleEventId = string.IsNullOrWhiteSpace(googleEventId) ? null : googleEventId,
            DeletedUtc = deletedUtc
        });
    }

    private static List<GoogleRemoteAction> DeduplicateActions(List<GoogleRemoteAction> actions)
    {
        // Per reminder id, keep strongest action; also collapse duplicate deletes by event id.
        var byReminder = new Dictionary<Guid, GoogleRemoteAction>();
        var deletesByEvent = new Dictionary<string, GoogleRemoteAction>(StringComparer.Ordinal);

        foreach (var action in actions)
        {
            if (action.Kind == GoogleRemoteActionKind.Delete
                && !string.IsNullOrWhiteSpace(action.GoogleEventId))
            {
                deletesByEvent[action.GoogleEventId!] = action;
                continue;
            }

            if (action.ReminderId == Guid.Empty)
            {
                continue;
            }

            if (!byReminder.TryGetValue(action.ReminderId, out var existing)
                || Rank(action.Kind) >= Rank(existing.Kind))
            {
                byReminder[action.ReminderId] = action;
            }
        }

        var result = byReminder.Values.ToList();
        foreach (var del in deletesByEvent.Values)
        {
            // Skip delete if same reminder already has create (shouldn't happen)
            if (del.ReminderId != Guid.Empty
                && byReminder.TryGetValue(del.ReminderId, out var other)
                && other.Kind == GoogleRemoteActionKind.Create)
            {
                continue;
            }

            result.Add(del);
        }

        return result;
    }

    private static int Rank(GoogleRemoteActionKind kind)
        => kind switch
        {
            GoogleRemoteActionKind.Delete => 3,
            GoogleRemoteActionKind.Update => 2,
            GoogleRemoteActionKind.Create => 1,
            _ => 0
        };
}
