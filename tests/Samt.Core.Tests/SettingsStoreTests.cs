using Samt.Core.Domain;
using Samt.Core.Locations;
using Samt.Core.Notifications;
using Samt.Core.Storage;

namespace Samt.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsActiveLocation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "samt-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var settings = SettingsJson.CreateDefault();
            var custom = new LocationProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test City",
                Latitude = 36.7,
                Longitude = 3.0,
                TimeZoneId = KnownLocations.AlgeriaTimeZoneId,
                Source = LocationSource.Manual
            };

            var updated = settings.With(
                locations: settings.Locations.Append(custom).ToList(),
                activeLocationId: custom.Id,
                language: "en-US",
                theme: "dark",
                asrMadhab: AsrMadhab.Hanafi);

            await store.SaveAsync(updated);
            var loaded = await store.LoadAsync();

            Assert.Equal(custom.Id, loaded.ActiveLocationId);
            Assert.Equal("en-US", loaded.Language);
            Assert.Equal("dark", loaded.Theme);
            Assert.Equal(AsrMadhab.Hanafi, loaded.AsrMadhab);
            Assert.Contains(loaded.Locations, l => l.Id == custom.Id && l.DisplayName == "Test City");
            Assert.Equal(36.7, loaded.Locations.First(l => l.Id == custom.Id).Latitude, 5);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Load_WhenMissing_WritesDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "samt-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var loaded = await store.LoadAsync();
            Assert.True(File.Exists(store.SettingsPath));
            Assert.NotEmpty(loaded.Locations);
            Assert.Equal(KnownLocations.Kennadsa.Id, loaded.ActiveLocationId);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void NormalizeClockTime_ParsesAndFallsBack()
    {
        Assert.Equal("06:30", SettingsJson.NormalizeClockTime("6:30", "06:00"));
        Assert.Equal("22:00", SettingsJson.NormalizeClockTime("22:00", "06:00"));
        Assert.Equal("06:00", SettingsJson.NormalizeClockTime("not-a-time", "06:00"));
        Assert.Equal("17:00", SettingsJson.NormalizeClockTime(null, "17:00"));
    }

    [Fact]
    public void CreateDefault_AdhkarTimesAndAfterPrayerDelay()
    {
        var settings = SettingsJson.CreateDefault();
        Assert.Equal("06:00", settings.AdhkarMorningTime);
        Assert.Equal("17:00", settings.AdhkarEveningTime);
        Assert.Equal("22:00", settings.AdhkarSleepTime);
        Assert.Equal(0, settings.AdhkarAfterPrayerDelayMinutes);

        var updated = settings.With(
            adhkarMorningTime: "5:15",
            adhkarAfterPrayerDelayMinutes: 10);
        var roundTrip = SettingsJson.Deserialize(SettingsJson.Serialize(updated));
        Assert.Equal("05:15", roundTrip.AdhkarMorningTime);
        Assert.Equal(10, roundTrip.AdhkarAfterPrayerDelayMinutes);
    }

    [Fact]
    public void CreateDefault_Phase8Fields()
    {
        var settings = SettingsJson.CreateDefault();
        Assert.True(settings.AdhkarAutoAdvanceEnabled);
        Assert.Equal(1.0, settings.WindowOpacity);
        Assert.False(settings.SetupWizardCompleted);

        var updated = settings.With(
            adhkarAutoAdvanceEnabled: false,
            windowOpacity: 0.55,
            setupWizardCompleted: true);
        var roundTrip = SettingsJson.Deserialize(SettingsJson.Serialize(updated));
        Assert.False(roundTrip.AdhkarAutoAdvanceEnabled);
        Assert.Equal(0.55, roundTrip.WindowOpacity, 3);
        Assert.True(roundTrip.SetupWizardCompleted);
    }

    [Fact]
    public void Normalize_ClampsWindowOpacity()
    {
        var settings = SettingsJson.CreateDefault().With(windowOpacity: 0.05);
        var normalized = SettingsJson.Normalize(settings);
        Assert.Equal(0.30, normalized.WindowOpacity, 3);

        var high = SettingsJson.Normalize(SettingsJson.CreateDefault().With(windowOpacity: 1.5));
        Assert.Equal(1.0, high.WindowOpacity, 3);
    }

    [Fact]
    public void CreateDefault_Phase9CalendarFields_AndAlgeriaSeedsHaveDz()
    {
        var settings = SettingsJson.CreateDefault();

        Assert.Null(settings.CalendarCountryOverride);
        Assert.False(settings.SpecialDayRemindersEnabled);
        Assert.False(settings.SpecialDayIslamicSetEnabled);
        Assert.False(settings.SpecialDayCountrySetEnabled);
        Assert.Equal("09:00", settings.SpecialDayReminderTime);
        Assert.Empty(settings.SpecialDayMutedIds);

        Assert.All(settings.Locations, loc => Assert.Equal(KnownLocations.AlgeriaCountryCode, loc.CountryCode));
        Assert.Equal("DZ", KnownLocations.Kennadsa.CountryCode);
        Assert.Equal("DZ", KnownLocations.Algiers.CountryCode);
        Assert.Equal("DZ", KnownLocations.Oran.CountryCode);
        Assert.Equal("DZ", KnownLocations.Bechar.CountryCode);
    }

    [Fact]
    public void CreateDefault_Phase9Fields_RoundTripThroughJson()
    {
        var updated = SettingsJson.CreateDefault().With(
            calendarCountryOverride: "dz",
            specialDayRemindersEnabled: true,
            specialDayIslamicSetEnabled: true,
            specialDayCountrySetEnabled: true,
            specialDayReminderTime: "8:30",
            specialDayMutedIds: ["islamic.eid_fitr", "  dz.labour  ", "islamic.eid_fitr", ""]);

        var roundTrip = SettingsJson.Deserialize(SettingsJson.Serialize(updated));

        Assert.Equal("DZ", roundTrip.CalendarCountryOverride);
        Assert.True(roundTrip.SpecialDayRemindersEnabled);
        Assert.True(roundTrip.SpecialDayIslamicSetEnabled);
        Assert.True(roundTrip.SpecialDayCountrySetEnabled);
        Assert.Equal("08:30", roundTrip.SpecialDayReminderTime);
        Assert.Equal(["islamic.eid_fitr", "dz.labour"], roundTrip.SpecialDayMutedIds);
        Assert.All(roundTrip.Locations, loc => Assert.Equal("DZ", loc.CountryCode));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsCountryCodeAndSpecialDayPrefs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "samt-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var custom = new LocationProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = "Paris",
                Latitude = 48.8566,
                Longitude = 2.3522,
                TimeZoneId = "Romance Standard Time",
                Source = LocationSource.Manual,
                CountryCode = "fr"
            };

            var settings = SettingsJson.CreateDefault().With(
                locations: SettingsJson.CreateDefault().Locations.Append(custom).ToList(),
                activeLocationId: custom.Id,
                calendarCountryOverride: "DZ",
                specialDayRemindersEnabled: true,
                specialDayIslamicSetEnabled: true,
                specialDayCountrySetEnabled: false,
                specialDayReminderTime: "09:15",
                specialDayMutedIds: ["islamic.mawlid"]);

            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            var paris = Assert.Single(loaded.Locations, l => l.Id == custom.Id);
            Assert.Equal("FR", paris.CountryCode);
            Assert.Equal("DZ", loaded.CalendarCountryOverride);
            Assert.True(loaded.SpecialDayRemindersEnabled);
            Assert.True(loaded.SpecialDayIslamicSetEnabled);
            Assert.False(loaded.SpecialDayCountrySetEnabled);
            Assert.Equal("09:15", loaded.SpecialDayReminderTime);
            Assert.Equal(["islamic.mawlid"], loaded.SpecialDayMutedIds);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void With_ClearCalendarCountryOverride()
    {
        var withOverride = SettingsJson.CreateDefault().With(calendarCountryOverride: "DZ");
        Assert.Equal("DZ", withOverride.CalendarCountryOverride);

        var cleared = withOverride.With(
            calendarCountryOverride: null,
            replaceCalendarCountryOverride: true);
        Assert.Null(cleared.CalendarCountryOverride);
    }

    [Fact]
    public void LegacyJson_WithoutPhase9Fields_DefaultsRemindersOff()
    {
        var legacy = """
            {
              "schemaVersion": 1,
              "language": "ar",
              "theme": "system",
              "locations": [
                {
                  "id": "a1111111-1111-4111-8111-111111111111",
                  "displayName": "القنادسة / Kennadsa",
                  "latitude": 31.5569,
                  "longitude": -2.4181,
                  "timeZoneId": "W. Central Africa Standard Time",
                  "source": "CitySeed"
                }
              ]
            }
            """;
        var loaded = SettingsJson.Deserialize(legacy);

        Assert.Null(loaded.CalendarCountryOverride);
        Assert.False(loaded.SpecialDayRemindersEnabled);
        Assert.False(loaded.SpecialDayIslamicSetEnabled);
        Assert.False(loaded.SpecialDayCountrySetEnabled);
        Assert.Equal("09:00", loaded.SpecialDayReminderTime);
        Assert.Empty(loaded.SpecialDayMutedIds);
        Assert.Null(loaded.Locations[0].CountryCode);
    }

    [Fact]
    public void NormalizeCountryCode_TrimsAndUppercases()
    {
        Assert.Equal("DZ", SettingsJson.NormalizeCountryCode(" dz "));
        Assert.Null(SettingsJson.NormalizeCountryCode(null));
        Assert.Null(SettingsJson.NormalizeCountryCode("  "));
    }

    [Fact]
    public void LegacyJson_WithoutPhase8Fields_SkipsWizardAndKeepsOpaque()
    {
        // Existing installs: missing SetupWizardCompleted must not force the wizard.
        var legacy = """
            {
              "schemaVersion": 1,
              "language": "ar",
              "theme": "system",
              "locations": []
            }
            """;
        var loaded = SettingsJson.Deserialize(legacy);
        Assert.True(loaded.SetupWizardCompleted);
        Assert.True(loaded.AdhkarAutoAdvanceEnabled);
        Assert.Equal(1.0, loaded.WindowOpacity, 3);
    }

    [Fact]
    public void CreateDefault_UsesPhase4NotificationChannels()
    {
        var settings = SettingsJson.CreateDefault();
        var start = Assert.Single(settings.NotificationRules, r => r.Kind == NotificationEventKind.PrayerStart);
        var generalBefore = Assert.Single(
            settings.NotificationRules,
            r => r.Kind == NotificationEventKind.BeforePrayer && r.Id == NotificationRulesComposer.BeforeGeneralRuleId);
        var fajrException = Assert.Single(
            settings.NotificationRules,
            r => r.Id == NotificationRulesComposer.BeforeExceptionId(PrayerEvent.Fajr));

        Assert.Equal(NotificationChannel.All, start.Channels);
        Assert.Equal(NotificationChannel.WindowsToast | NotificationChannel.Overlay, generalBefore.Channels);
        Assert.Equal(15, generalBefore.OffsetMinutes);
        Assert.Equal(30, fajrException.OffsetMinutes);
        Assert.Equal(AudioSource.Library, settings.DefaultAudio.Source);
        Assert.Equal(BuiltInSoundIds.AdhanAlaqsa, settings.DefaultAudio.SoundId);
        Assert.Equal(BuiltInSoundIds.AdhanAlaqsa, settings.AdhanSoundId);
        Assert.Equal(BuiltInSoundIds.Takbir, settings.PreAlertSoundId);
        Assert.True(settings.DefaultOverlay.Enabled);
        Assert.Equal(OverlayEdge.Bottom, settings.DefaultOverlay.EntryEdge);
        Assert.InRange(settings.DefaultOverlay.Opacity, 0.3, 1.0);
        Assert.True(settings.DefaultOverlay.AnimationDuration.TotalMilliseconds > 0);
        Assert.Empty(settings.MinuteAdjustments);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsMinuteAdjustments()
    {
        var dir = Path.Combine(Path.GetTempPath(), "samt-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var settings = SettingsJson.CreateDefault().With(
                minuteAdjustments: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [PrayerEvent.Fajr.ToString()] = 2,
                    [PrayerEvent.Maghrib.ToString()] = -1
                });

            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            Assert.True(loaded.HasManualAdjustment(PrayerEvent.Fajr));
            Assert.Equal(2, loaded.GetMinuteAdjustment(PrayerEvent.Fajr));
            Assert.Equal(-1, loaded.GetMinuteAdjustment(PrayerEvent.Maghrib));
            Assert.False(loaded.HasManualAdjustment(PrayerEvent.Dhuhr));

            var profile = loaded.GetActiveCalculationProfile();
            Assert.Equal(2, profile.GetAdjustment(PrayerEvent.Fajr));
            Assert.Equal(-1, profile.GetAdjustment(PrayerEvent.Maghrib));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Normalize_UpgradesLegacyToastOnlyDefaultRules()
    {
        var defaults = SettingsJson.CreateDefault();
        var legacyRules = new[]
        {
            new NotificationRule
            {
                Id = Guid.Parse("b1111111-1111-4111-8111-111111111111"),
                Kind = NotificationEventKind.PrayerStart,
                TargetPrayers = [PrayerEvent.Fajr],
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            },
            new NotificationRule
            {
                Id = Guid.Parse("b2222222-2222-4222-8222-222222222222"),
                Kind = NotificationEventKind.BeforePrayer,
                TargetPrayers = [PrayerEvent.Fajr],
                OffsetMinutes = 15,
                Channels = NotificationChannel.WindowsToast,
                Enabled = true
            }
        };

        var json = SettingsJson.Serialize(new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Language = "ar",
            Theme = "system",
            ActiveLocationId = defaults.ActiveLocationId,
            ActiveCalculationProfileId = defaults.ActiveCalculationProfileId,
            Locations = defaults.Locations,
            NotificationRules = legacyRules,
            DefaultAudio = new AudioProfile { Source = AudioSource.WindowsDefault },
            DefaultOverlay = new OverlayProfile()
        });
        var loaded = SettingsJson.Deserialize(json);
        var start = Assert.Single(loaded.NotificationRules, r => r.Kind == NotificationEventKind.PrayerStart);
        var before = Assert.Single(loaded.NotificationRules, r => r.Kind == NotificationEventKind.BeforePrayer);
        Assert.Equal(NotificationChannel.All, start.Channels);
        Assert.Equal(NotificationChannel.WindowsToast | NotificationChannel.Overlay, before.Channels);
    }

    [Fact]
    public async Task Load_FallsBackToBackup_WhenPrimaryCorrupt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "samt-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var settings = SettingsJson.CreateDefault().With(theme: "light");
            await store.SaveAsync(settings);

            await File.WriteAllTextAsync(store.SettingsPath, "{ not-json");
            var loaded = await store.LoadAsync();
            Assert.Equal("light", loaded.Theme);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
