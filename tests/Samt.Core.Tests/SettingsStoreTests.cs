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
