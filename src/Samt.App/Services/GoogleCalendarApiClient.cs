using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Samt.Core.Domain;
using Samt.Core.Formatting;
using Samt.Core.Locations;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>Thin Google Calendar API adapter (network I/O only).</summary>
public sealed class GoogleCalendarApiClient : IAsyncDisposable
{
    public const string SamtCalendarSummary = "SAMT";
    public const string ExtendedPropertyKey = "samtReminderId";

    private static readonly string[] Scopes =
    [
        CalendarService.Scope.Calendar,
        "https://www.googleapis.com/auth/userinfo.email"
    ];

    private readonly GoogleOAuthTokenStore _tokenStore;
    private CalendarService? _service;
    private UserCredential? _credential;

    public GoogleCalendarApiClient(GoogleOAuthTokenStore? tokenStore = null)
    {
        _tokenStore = tokenStore ?? new GoogleOAuthTokenStore();
    }

    public bool IsAuthenticated => _credential is not null && _service is not null;

    public async Task<string> AuthorizeInteractiveAsync(CancellationToken ct = default)
    {
        if (!GoogleOAuthClientConfig.TryLoad(out var clientId, out var clientSecret, out var err))
        {
            throw new InvalidOperationException(err ?? "missing-client");
        }

        // Drop any cached token so Connect always opens the browser and can upgrade scopes.
        // Stale tokens without Calendar scope cause silent reuse + Forbidden insufficient scopes.
        await ClearSessionAsync().ConfigureAwait(false);

        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "samt-user",
            ct,
            _tokenStore).ConfigureAwait(false);

        _service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential,
            ApplicationName = "SAMT"
        });

        return await TryGetEmailAsync(ct).ConfigureAwait(false) ?? "";
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        if (!GoogleOAuthClientConfig.TryLoad(out var clientId, out var clientSecret, out _))
        {
            return false;
        }

        if (!await _tokenStore.HasUserCredentialAsync().ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "samt-user",
                ct,
                _tokenStore).ConfigureAwait(false);

            if (_credential.Token.IsStale)
            {
                await _credential.RefreshTokenAsync(ct).ConfigureAwait(false);
            }

            _service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "SAMT"
            });
            return true;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"GoogleCalendar restore session failed: {ex.Message}");
            return false;
        }
    }

    public async Task ClearSessionAsync()
    {
        await _tokenStore.ClearAsync().ConfigureAwait(false);
        _service?.Dispose();
        _service = null;
        _credential = null;
    }

    public async Task<string> FindOrCreateSamtCalendarAsync(CancellationToken ct = default)
    {
        EnsureService();
        string? pageToken = null;
        do
        {
            var list = _service!.CalendarList.List();
            list.PageToken = pageToken;
            var response = await list.ExecuteAsync(ct).ConfigureAwait(false);
            foreach (var entry in response.Items ?? [])
            {
                if (string.Equals(entry.Summary, SamtCalendarSummary, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Id))
                {
                    return entry.Id;
                }
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        var created = await _service.Calendars.Insert(new Google.Apis.Calendar.v3.Data.Calendar
        {
            Summary = SamtCalendarSummary,
            Description = "SAMT user calendar reminders (app-managed)"
        }).ExecuteAsync(ct).ConfigureAwait(false);

        return created.Id
               ?? throw new InvalidOperationException("Google calendar create returned no id.");
    }

    public async Task DeleteCalendarAsync(string calendarId, CancellationToken ct = default)
    {
        EnsureService();
        if (string.IsNullOrWhiteSpace(calendarId))
        {
            return;
        }

        await _service!.Calendars.Delete(calendarId).ExecuteAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GoogleCalendarEventSnapshot>> ListEventsAsync(
        string calendarId,
        TimeZoneInfo locationTimeZone,
        CancellationToken ct = default)
    {
        EnsureService();
        var results = new List<GoogleCalendarEventSnapshot>();
        string? pageToken = null;
        var timeMin = DateTime.UtcNow.AddYears(-1);
        var timeMax = DateTime.UtcNow.AddYears(2);

        do
        {
            var request = _service!.Events.List(calendarId);
            request.PageToken = pageToken;
            request.SingleEvents = false;
            request.ShowDeleted = true;
            request.TimeMinDateTimeOffset = new DateTimeOffset(timeMin, TimeSpan.Zero);
            request.TimeMaxDateTimeOffset = new DateTimeOffset(timeMax, TimeSpan.Zero);
            request.MaxResults = 250;

            var response = await request.ExecuteAsync(ct).ConfigureAwait(false);
            foreach (var ev in response.Items ?? [])
            {
                if (string.IsNullOrWhiteSpace(ev.Id))
                {
                    continue;
                }

                results.Add(MapEvent(ev, locationTimeZone));
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return results;
    }

    public async Task<string> CreateEventAsync(
        string calendarId,
        UserCalendarReminder reminder,
        TimeZoneInfo locationTimeZone,
        CancellationToken ct = default)
    {
        EnsureService();
        var body = ToEvent(reminder, locationTimeZone);
        var created = await _service!.Events.Insert(body, calendarId).ExecuteAsync(ct).ConfigureAwait(false);
        return created.Id ?? throw new InvalidOperationException("Create event returned no id.");
    }

    public async Task UpdateEventAsync(
        string calendarId,
        string eventId,
        UserCalendarReminder reminder,
        TimeZoneInfo locationTimeZone,
        CancellationToken ct = default)
    {
        EnsureService();
        var body = ToEvent(reminder, locationTimeZone);
        await _service!.Events.Update(body, calendarId, eventId).ExecuteAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteEventAsync(string calendarId, string eventId, CancellationToken ct = default)
    {
        EnsureService();
        try
        {
            await _service!.Events.Delete(calendarId, eventId).ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound
                                                    || ex.HttpStatusCode == System.Net.HttpStatusCode.Gone)
        {
            // already gone
        }
    }

    private async Task<string?> TryGetEmailAsync(CancellationToken ct)
    {
        try
        {
            // oauth2 userinfo via credential token
            if (_credential?.Token?.AccessToken is null)
            {
                return null;
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _credential.Token.AccessToken);
            var json = await http.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo", ct)
                .ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("email", out var email))
            {
                return email.GetString();
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Google email lookup failed: {ex.Message}");
        }

        return null;
    }

    private void EnsureService()
    {
        if (_service is null)
        {
            throw new InvalidOperationException("Google Calendar not authenticated.");
        }
    }

    private static Event ToEvent(UserCalendarReminder reminder, TimeZoneInfo tz)
    {
        var time = TimeOnly.Parse(reminder.Time, CultureInfo.InvariantCulture);
        var localStart = reminder.CivilDate.ToDateTime(time);
        var dto = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart));
        var end = dto.AddMinutes(30);
        // Google Calendar requires IANA ids (e.g. Africa/Algiers), not Windows ids.
        var tzId = ToGoogleTimeZoneId(tz);

        return new Event
        {
            Summary = reminder.Title,
            Description = reminder.Note,
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = dto,
                TimeZone = tzId
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = end,
                TimeZone = tzId
            },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    [ExtendedPropertyKey] = reminder.Id.ToString("N")
                }
            }
        };
    }

    /// <summary>
    /// Maps a Windows <see cref="TimeZoneInfo"/> to an IANA id accepted by Google Calendar.
    /// </summary>
    internal static string ToGoogleTimeZoneId(TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(tz);
        return KnownLocations.ToIanaTimeZoneId(tz.Id);
    }

    private static GoogleCalendarEventSnapshot MapEvent(Event ev, TimeZoneInfo locationTimeZone)
    {
        var isCancelled = string.Equals(ev.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
        var isRecurring = !string.IsNullOrWhiteSpace(ev.Recurrence?.FirstOrDefault())
                          || !string.IsNullOrWhiteSpace(ev.RecurringEventId);
        var isAllDay = ev.Start?.Date is not null && ev.Start.DateTimeDateTimeOffset is null;

        DateOnly? civil = null;
        string? time = null;

        if (ev.Start?.DateTimeDateTimeOffset is { } startDto)
        {
            var local = TimeZoneInfo.ConvertTime(startDto, locationTimeZone);
            civil = DateOnly.FromDateTime(local.DateTime);
            time = LatinDigits.EnsureLatin(local.ToString("HH:mm", CultureInfo.InvariantCulture));
        }
        else if (!string.IsNullOrWhiteSpace(ev.Start?.Date)
                 && DateOnly.TryParse(ev.Start.Date, CultureInfo.InvariantCulture, out var d))
        {
            civil = d;
            isAllDay = true;
        }

        var updated = ev.UpdatedDateTimeOffset ?? DateTimeOffset.UtcNow;

        Guid? samtReminderId = null;
        if (ev.ExtendedProperties?.Private__ is { } priv
            && priv.TryGetValue(ExtendedPropertyKey, out var raw)
            && Guid.TryParse(raw, out var parsed)
            && parsed != Guid.Empty)
        {
            samtReminderId = parsed;
        }

        return new GoogleCalendarEventSnapshot
        {
            EventId = ev.Id!,
            Title = ev.Summary ?? "",
            Note = ev.Description ?? "",
            CivilDate = civil,
            Time = time,
            UpdatedUtc = updated.ToUniversalTime(),
            IsCancelled = isCancelled,
            IsAllDay = isAllDay,
            IsRecurring = isRecurring,
            SamtReminderId = samtReminderId
        };
    }

    public ValueTask DisposeAsync()
    {
        _service?.Dispose();
        _service = null;
        return ValueTask.CompletedTask;
    }
}
