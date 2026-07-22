using Samt.Core.Calendar;
using Samt.Core.Domain;

namespace Samt.Core.Tests;

public class GoogleCalendarSyncPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Plan_UnlinkedLocal_CreatesRemoteAction()
    {
        var local = Reminder("Dentist", new DateOnly(2026, 7, 20), "09:00", Now.AddHours(-1));

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [], Now);

        Assert.Single(plan.RemoteActions);
        Assert.Equal(GoogleRemoteActionKind.Create, plan.RemoteActions[0].Kind);
        Assert.Equal(local.Id, plan.RemoteActions[0].ReminderId);
        Assert.Single(plan.Reminders);
    }

    [Fact]
    public void Plan_ImportableRemote_WithoutLocal_Imports()
    {
        var remote = TimedEvent("g1", "From Google", new DateOnly(2026, 8, 1), "15:30", Now.AddMinutes(-5));

        var plan = GoogleCalendarSyncPlanner.Plan([], [], [remote], Now);

        Assert.Single(plan.Reminders);
        Assert.Equal("From Google", plan.Reminders[0].Title);
        Assert.Equal("g1", plan.Reminders[0].GoogleEventId);
        Assert.Equal(1, plan.ImportedCount);
        Assert.Empty(plan.RemoteActions);
    }

    [Fact]
    public void Plan_AllDayRemote_IsSkipped()
    {
        var remote = new GoogleCalendarEventSnapshot
        {
            EventId = "all1",
            Title = "Travel",
            CivilDate = new DateOnly(2026, 8, 2),
            IsAllDay = true,
            UpdatedUtc = Now
        };

        var plan = GoogleCalendarSyncPlanner.Plan([], [], [remote], Now);

        Assert.Empty(plan.Reminders);
        Assert.Single(plan.SkipReasons);
        Assert.Contains("all-day", plan.SkipReasons[0]);
    }

    [Fact]
    public void Plan_RecurringRemote_IsSkipped()
    {
        var remote = new GoogleCalendarEventSnapshot
        {
            EventId = "r1",
            Title = "Weekly",
            CivilDate = new DateOnly(2026, 8, 3),
            Time = "10:00",
            IsRecurring = true,
            UpdatedUtc = Now
        };

        var plan = GoogleCalendarSyncPlanner.Plan([], [], [remote], Now);

        Assert.Empty(plan.Reminders);
        Assert.Contains(plan.SkipReasons, s => s.StartsWith("recurring", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_RemoteNewer_WinsWholeEvent()
    {
        var local = Reminder(
            "Old title",
            new DateOnly(2026, 7, 20),
            "09:00",
            Now.AddHours(-2),
            googleEventId: "g-linked",
            lastSynced: Now.AddHours(-2));

        var remote = TimedEvent(
            "g-linked",
            "New title",
            new DateOnly(2026, 7, 21),
            "11:00",
            Now.AddMinutes(-1),
            note: "from phone");

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [remote], Now);

        Assert.Single(plan.Reminders);
        Assert.Equal("New title", plan.Reminders[0].Title);
        Assert.Equal(new DateOnly(2026, 7, 21), plan.Reminders[0].CivilDate);
        Assert.Equal("11:00", plan.Reminders[0].Time);
        Assert.Equal("from phone", plan.Reminders[0].Note);
        Assert.Equal(1, plan.UpdatedLocalCount);
        Assert.Empty(plan.RemoteActions);
    }

    [Fact]
    public void Plan_LocalNewer_PushesUpdate()
    {
        var local = Reminder(
            "Local title",
            new DateOnly(2026, 7, 20),
            "09:00",
            Now.AddMinutes(-1),
            googleEventId: "g-linked",
            lastSynced: Now.AddHours(-2));

        var remote = TimedEvent(
            "g-linked",
            "Remote title",
            new DateOnly(2026, 7, 20),
            "09:00",
            Now.AddHours(-3));

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [remote], Now);

        Assert.Single(plan.Reminders);
        Assert.Equal("Local title", plan.Reminders[0].Title);
        Assert.Single(plan.RemoteActions);
        Assert.Equal(GoogleRemoteActionKind.Update, plan.RemoteActions[0].Kind);
    }

    [Fact]
    public void Plan_Tombstone_DeletesRemoteAndDoesNotImport()
    {
        var stones = new[]
        {
            new CalendarSyncTombstone
            {
                ReminderId = Guid.NewGuid(),
                GoogleEventId = "g-dead",
                DeletedUtc = Now.AddDays(-1)
            }
        };
        var remote = TimedEvent("g-dead", "Resurrect?", new DateOnly(2026, 7, 20), "09:00", Now);

        var plan = GoogleCalendarSyncPlanner.Plan([], stones, [remote], Now);

        Assert.Empty(plan.Reminders);
        Assert.Single(plan.RemoteActions);
        Assert.Equal(GoogleRemoteActionKind.Delete, plan.RemoteActions[0].Kind);
        Assert.Equal("g-dead", plan.RemoteActions[0].GoogleEventId);
    }

    [Fact]
    public void Plan_RemoteMissingAfterSynced_DeletesLocal()
    {
        var local = Reminder(
            "Gone",
            new DateOnly(2026, 7, 20),
            "09:00",
            Now.AddDays(-1),
            googleEventId: "g-gone",
            lastSynced: Now.AddDays(-1));

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [], Now);

        Assert.Empty(plan.Reminders);
        Assert.Equal(1, plan.DeletedLocalCount);
        Assert.Contains(plan.Tombstones, t => t.GoogleEventId == "g-gone");
    }

    [Fact]
    public void Plan_RemoteCancelled_DeletesLinkedLocal()
    {
        var local = Reminder(
            "Cancel me",
            new DateOnly(2026, 7, 20),
            "09:00",
            Now.AddDays(-1),
            googleEventId: "g-cancel",
            lastSynced: Now.AddDays(-1));

        var remote = new GoogleCalendarEventSnapshot
        {
            EventId = "g-cancel",
            Title = "Cancel me",
            CivilDate = new DateOnly(2026, 7, 20),
            Time = "09:00",
            IsCancelled = true,
            UpdatedUtc = Now
        };

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [remote], Now);

        Assert.Empty(plan.Reminders);
        Assert.Equal(1, plan.DeletedLocalCount);
    }

    [Fact]
    public void Plan_PreservesLocalEnabledAndRepeats_WhenRemoteWins()
    {
        var local = new UserCalendarReminder
        {
            Id = Guid.NewGuid(),
            Title = "Meds",
            Note = "",
            CivilDate = new DateOnly(2026, 7, 20),
            Time = "09:00",
            RepeatCount = 3,
            IntervalMinutes = 10,
            Enabled = false,
            GoogleEventId = "g-meds",
            LocalUpdatedUtc = Now.AddHours(-2),
            LastSyncedUtc = Now.AddHours(-2)
        };
        var remote = TimedEvent("g-meds", "Meds updated", new DateOnly(2026, 7, 20), "10:00", Now);

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [remote], Now);

        Assert.Equal(3, plan.Reminders[0].RepeatCount);
        Assert.Equal(10, plan.Reminders[0].IntervalMinutes);
        Assert.False(plan.Reminders[0].Enabled);
        Assert.Equal("Meds updated", plan.Reminders[0].Title);
        Assert.Equal("10:00", plan.Reminders[0].Time);
    }

    [Fact]
    public void TombstoneDelete_AddsEntry()
    {
        var id = Guid.NewGuid();
        var stones = GoogleCalendarSyncPlanner.TombstoneDelete([], id, "g1", Now);
        Assert.Single(stones);
        Assert.Equal(id, stones[0].ReminderId);
        Assert.Equal("g1", stones[0].GoogleEventId);
    }

    [Fact]
    public void Plan_RematchBySamtReminderId_RelinksWithoutDuplicateCreate()
    {
        var id = Guid.NewGuid();
        var local = new UserCalendarReminder
        {
            Id = id,
            Title = "Dentist",
            Note = "",
            CivilDate = new DateOnly(2026, 7, 20),
            Time = "09:00",
            LocalUpdatedUtc = Now.AddHours(-1),
            GoogleEventId = null,
            LastSyncedUtc = null
        };
        var remote = new GoogleCalendarEventSnapshot
        {
            EventId = "g-old",
            Title = "Dentist",
            CivilDate = new DateOnly(2026, 7, 20),
            Time = "09:00",
            UpdatedUtc = Now.AddHours(-2),
            SamtReminderId = id
        };

        var plan = GoogleCalendarSyncPlanner.Plan([local], [], [remote], Now);

        Assert.Single(plan.Reminders);
        Assert.Equal(id, plan.Reminders[0].Id);
        Assert.Equal("g-old", plan.Reminders[0].GoogleEventId);
        Assert.Equal(0, plan.ImportedCount);
        Assert.DoesNotContain(plan.RemoteActions, a => a.Kind == GoogleRemoteActionKind.Create);
        // Local newer than remote → push update
        Assert.Contains(plan.RemoteActions, a =>
            a.Kind == GoogleRemoteActionKind.Update && a.GoogleEventId == "g-old");
    }

    private static UserCalendarReminder Reminder(
        string title,
        DateOnly date,
        string time,
        DateTimeOffset localUpdated,
        string? googleEventId = null,
        DateTimeOffset? lastSynced = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Note = "",
            CivilDate = date,
            Time = time,
            GoogleEventId = googleEventId,
            LocalUpdatedUtc = localUpdated,
            LastSyncedUtc = lastSynced
        };

    private static GoogleCalendarEventSnapshot TimedEvent(
        string id,
        string title,
        DateOnly date,
        string time,
        DateTimeOffset updated,
        string note = "")
        => new()
        {
            EventId = id,
            Title = title,
            Note = note,
            CivilDate = date,
            Time = time,
            UpdatedUtc = updated
        };
}
