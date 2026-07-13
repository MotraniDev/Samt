using Samt.Core.Domain;
using Samt.Core.Locations;
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
    public void CreateDefault_UsesPhase4NotificationChannels()
    {
        var settings = SettingsJson.CreateDefault();
        var start = Assert.Single(settings.NotificationRules, r => r.Kind == NotificationEventKind.PrayerStart);
        var before = Assert.Single(settings.NotificationRules, r => r.Kind == NotificationEventKind.BeforePrayer);

        Assert.Equal(NotificationChannel.All, start.Channels);
        Assert.Equal(NotificationChannel.WindowsToast | NotificationChannel.Overlay, before.Channels);
        Assert.Equal(AudioSource.WindowsDefault, settings.DefaultAudio.Source);
        Assert.True(settings.DefaultOverlay.Enabled);
        Assert.Equal(OverlayEdge.Bottom, settings.DefaultOverlay.EntryEdge);
        Assert.InRange(settings.DefaultOverlay.Opacity, 0.3, 1.0);
        Assert.True(settings.DefaultOverlay.AnimationDuration.TotalMilliseconds > 0);
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
