using System.Globalization;
using Samt.Core.Calendar;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Host for Google Calendar link: connect/disconnect/sync with local-first queue semantics.
/// </summary>
public sealed class GoogleCalendarLinkService : IAsyncDisposable
{
    public static readonly TimeSpan PullInterval = TimeSpan.FromMinutes(20);

    private readonly AppState _state;
    private readonly GoogleCalendarApiClient _api;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _syncRequested;

    public GoogleCalendarLinkService(AppState state, GoogleCalendarApiClient? api = null)
    {
        _state = state;
        _api = api ?? new GoogleCalendarApiClient();
    }

    public event EventHandler? StatusChanged;

    public bool IsBusy { get; private set; }

    /// <summary>connect | sync | disconnect — for UI busy copy while <see cref="IsBusy"/>.</summary>
    public string BusyPhase { get; private set; } = "";

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!_state.Settings.GoogleCalendarLink.IsLinked)
        {
            return;
        }

        if (await _api.TryRestoreSessionAsync(ct).ConfigureAwait(false))
        {
            StartBackgroundLoop();
            RequestSync();
        }
        else
        {
            await _state.UpdateAsync(s => s.With(googleCalendarLink: s.GoogleCalendarLink.With(
                isLinked: false,
                lastSyncSucceeded: false,
                lastSyncMessage: "session-expired",
                replaceLastSyncUtc: true,
                lastSyncUtc: DateTimeOffset.UtcNow))).ConfigureAwait(false);
            RaiseStatus();
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IsBusy = true;
            BusyPhase = "connect";
            RaiseStatus();

            if (!GoogleOAuthClientConfig.TryLoad(out _, out _, out var err))
            {
                await PersistStatusAsync(false, err ?? "missing-client", 0).ConfigureAwait(false);
                return false;
            }

            var email = await _api.AuthorizeInteractiveAsync(ct).ConfigureAwait(false);
            var calendarId = await _api.FindOrCreateSamtCalendarAsync(ct).ConfigureAwait(false);

            await _state.UpdateAsync(s => s.With(googleCalendarLink: new GoogleCalendarLinkState
            {
                IsLinked = true,
                AccountEmail = string.IsNullOrWhiteSpace(email) ? null : email,
                CalendarId = calendarId,
                LastSyncUtc = null,
                LastSyncSucceeded = false,
                LastSyncMessage = "",
                LastSkippedCount = 0
            })).ConfigureAwait(false);

            StartBackgroundLoop();
            // First connect: push all local then pull (planner does create for unlinked).
            await SyncCoreAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"GoogleCalendar Connect failed: {ex}");
            // Drop bad token so the next Connect re-prompts instead of silently reusing it.
            try
            {
                await _api.ClearSessionAsync().ConfigureAwait(false);
            }
            catch (Exception clearEx)
            {
                LaunchLog.Write($"GoogleCalendar clear after connect failure: {clearEx.Message}");
            }

            var message = IsInsufficientScopes(ex)
                ? "insufficient-scopes"
                : "connect-failed:" + Truncate(ex.Message, 180);
            await PersistStatusAsync(false, message, 0).ConfigureAwait(false);
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyPhase = "";
            RaiseStatus();
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(bool deleteCloudCalendar, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IsBusy = true;
            BusyPhase = "disconnect";
            RaiseStatus();
            StopBackgroundLoop();

            var calendarId = _state.Settings.GoogleCalendarLink.CalendarId;
            if (deleteCloudCalendar && !string.IsNullOrWhiteSpace(calendarId))
            {
                try
                {
                    if (!_api.IsAuthenticated)
                    {
                        await _api.TryRestoreSessionAsync(ct).ConfigureAwait(false);
                    }

                    if (_api.IsAuthenticated)
                    {
                        await _api.DeleteCalendarAsync(calendarId, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        LaunchLog.Write("Delete SAMT Google calendar skipped: not authenticated.");
                    }
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"Delete SAMT Google calendar failed: {ex.Message}");
                }
            }

            await _api.ClearSessionAsync().ConfigureAwait(false);

            // Clear link metadata + google event ids on reminders; keep local reminder content.
            await _state.UpdateAsync(s =>
            {
                var reminders = s.UserCalendarReminders.Select(r => new UserCalendarReminder
                {
                    Id = r.Id,
                    Title = r.Title,
                    Note = r.Note,
                    CivilDate = r.CivilDate,
                    Time = r.Time,
                    RepeatCount = r.RepeatCount,
                    IntervalMinutes = r.IntervalMinutes,
                    Enabled = r.Enabled,
                    GoogleEventId = null,
                    LocalUpdatedUtc = r.LocalUpdatedUtc,
                    LastSyncedUtc = null
                }).ToList();

                return s.With(
                    userCalendarReminders: reminders,
                    calendarSyncTombstones: [],
                    googleCalendarLink: new GoogleCalendarLinkState
                    {
                        IsLinked = false,
                        LastSyncMessage = deleteCloudCalendar ? "disconnected-deleted-cloud" : "disconnected",
                        LastSyncUtc = DateTimeOffset.UtcNow,
                        LastSyncSucceeded = true
                    });
            }).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            BusyPhase = "";
            RaiseStatus();
            _gate.Release();
        }
    }

    public void RequestSync()
    {
        Interlocked.Exchange(ref _syncRequested, 1);
    }

    public async Task SyncNowAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IsBusy = true;
            BusyPhase = "sync";
            RaiseStatus();
            await SyncCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            BusyPhase = "";
            RaiseStatus();
            _gate.Release();
        }
    }

    public void NotifyLocalReminderChanged()
    {
        if (_state.Settings.GoogleCalendarLink.IsLinked)
        {
            RequestSync();
        }
    }

    private void StartBackgroundLoop()
    {
        if (_loopTask is { IsCompleted: false })
        {
            return;
        }

        _loopCts = new CancellationTokenSource();
        var ct = _loopCts.Token;
        _loopTask = Task.Run(async () =>
        {
            var lastPull = DateTimeOffset.MinValue;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var pullDue = DateTimeOffset.UtcNow - lastPull >= PullInterval;
                    var requested = Interlocked.Exchange(ref _syncRequested, 0) == 1;
                    if ((requested || pullDue) && _state.Settings.GoogleCalendarLink.IsLinked)
                    {
                        if (await _gate.WaitAsync(0, ct).ConfigureAwait(false))
                        {
                            try
                            {
                                IsBusy = true;
                                BusyPhase = "sync";
                                RaiseStatus();
                                await SyncCoreAsync(ct).ConfigureAwait(false);
                                lastPull = DateTimeOffset.UtcNow;
                            }
                            finally
                            {
                                IsBusy = false;
                                BusyPhase = "";
                                RaiseStatus();
                                _gate.Release();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"GoogleCalendar loop: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    private void StopBackgroundLoop()
    {
        try
        {
            _loopCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        _loopCts = null;
        _loopTask = null;
    }

    private async Task SyncCoreAsync(CancellationToken ct)
    {
        if (!_state.Settings.GoogleCalendarLink.IsLinked)
        {
            return;
        }

        try
        {
            if (!_api.IsAuthenticated
                && !await _api.TryRestoreSessionAsync(ct).ConfigureAwait(false))
            {
                await PersistStatusAsync(false, "not-authenticated", 0).ConfigureAwait(false);
                return;
            }

            var link = _state.Settings.GoogleCalendarLink;
            var calendarId = link.CalendarId;
            if (string.IsNullOrWhiteSpace(calendarId))
            {
                calendarId = await _api.FindOrCreateSamtCalendarAsync(ct).ConfigureAwait(false);
                await _state.UpdateAsync(s => s.With(googleCalendarLink: s.GoogleCalendarLink.With(
                    calendarId: calendarId))).ConfigureAwait(false);
            }

            var tz = ResolveActiveTimeZone();
            var remote = await _api.ListEventsAsync(calendarId!, tz, ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            var plan = GoogleCalendarSyncPlanner.Plan(
                _state.Settings.UserCalendarReminders,
                _state.Settings.CalendarSyncTombstones,
                remote,
                now);

            // Apply local plan first
            var reminders = plan.Reminders.ToList();
            var reminderMap = reminders.ToDictionary(r => r.Id);

            // Execute remote actions
            var remoteFailures = 0;
            string? firstRemoteError = null;
            foreach (var action in plan.RemoteActions)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    switch (action.Kind)
                    {
                        case GoogleRemoteActionKind.Create when action.Reminder is not null:
                        {
                            var eventId = await _api.CreateEventAsync(calendarId!, action.Reminder, tz, ct)
                                .ConfigureAwait(false);
                            if (reminderMap.TryGetValue(action.ReminderId, out var r))
                            {
                                var linked = GoogleCalendarSyncPlanner.WithGoogleLink(r, eventId, now);
                                reminderMap[action.ReminderId] = linked;
                                Replace(reminders, linked);
                            }

                            break;
                        }
                        case GoogleRemoteActionKind.Update
                            when action.Reminder is not null && !string.IsNullOrWhiteSpace(action.GoogleEventId):
                        {
                            await _api.UpdateEventAsync(calendarId!, action.GoogleEventId!, action.Reminder, tz, ct)
                                .ConfigureAwait(false);
                            if (reminderMap.TryGetValue(action.ReminderId, out var r))
                            {
                                var linked = GoogleCalendarSyncPlanner.WithGoogleLink(r, action.GoogleEventId!, now);
                                reminderMap[action.ReminderId] = linked;
                                Replace(reminders, linked);
                            }

                            break;
                        }
                        case GoogleRemoteActionKind.Delete when !string.IsNullOrWhiteSpace(action.GoogleEventId):
                            await _api.DeleteEventAsync(calendarId!, action.GoogleEventId!, ct).ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    remoteFailures++;
                    firstRemoteError ??= Truncate(ex.Message, 120);
                    LaunchLog.Write($"Google remote action {action.Kind} failed: {ex.Message}");
                    // keep local; will retry next sync
                }
            }

            var skipped = plan.SkipReasons.Count;
            var ok = remoteFailures == 0;
            var msg = string.Create(
                CultureInfo.InvariantCulture,
                $"{(ok ? "ok" : "partial")} imported={plan.ImportedCount} localUpd={plan.UpdatedLocalCount} localDel={plan.DeletedLocalCount} remoteOps={plan.RemoteActions.Count} remoteFail={remoteFailures} skipped={skipped}");
            if (!ok && !string.IsNullOrWhiteSpace(firstRemoteError))
            {
                msg += " · " + firstRemoteError;
            }

            await _state.UpdateAsync(s => s.With(
                userCalendarReminders: reminders,
                calendarSyncTombstones: plan.Tombstones,
                googleCalendarLink: s.GoogleCalendarLink.With(
                    isLinked: true,
                    calendarId: calendarId,
                    lastSyncUtc: now,
                    lastSyncSucceeded: ok,
                    lastSyncMessage: msg,
                    lastSkippedCount: skipped))).ConfigureAwait(false);

            RaiseStatus();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"GoogleCalendar Sync failed: {ex}");
            await PersistStatusAsync(false, "sync-failed:" + ex.Message, 0).ConfigureAwait(false);
        }
    }

    private static void Replace(List<UserCalendarReminder> list, UserCalendarReminder item)
    {
        var i = list.FindIndex(r => r.Id == item.Id);
        if (i >= 0)
        {
            list[i] = item;
        }
        else
        {
            list.Add(item);
        }
    }

    private TimeZoneInfo ResolveActiveTimeZone()
    {
        try
        {
            var loc = _state.RequireActiveLocation();
            return KnownLocations.ResolveTimeZone(loc.TimeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }

    private async Task PersistStatusAsync(bool success, string message, int skipped)
    {
        await _state.UpdateAsync(s => s.With(googleCalendarLink: s.GoogleCalendarLink.With(
            lastSyncUtc: DateTimeOffset.UtcNow,
            replaceLastSyncUtc: true,
            lastSyncSucceeded: success,
            lastSyncMessage: message,
            lastSkippedCount: skipped))).ConfigureAwait(false);
        RaiseStatus();
    }

    private static bool IsInsufficientScopes(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase)
               || text.Contains("insufficientScope", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var t = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= max ? t : t[..max] + "…";
    }

    private void RaiseStatus() => StatusChanged?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        StopBackgroundLoop();
        await _api.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
