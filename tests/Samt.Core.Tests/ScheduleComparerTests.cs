using Samt.Core.Calculation;
using Samt.Core.Domain;
using Samt.Core.Locations;

namespace Samt.Core.Tests;

public class ScheduleComparerTests
{
    [Fact]
    public void SelfComparison_HasZeroDeltas()
    {
        var engine = new PrayerEngine();
        var location = KnownLocations.Kennadsa;
        var profile = CalculationMethods.Algeria;

        var days = Enumerable.Range(0, 30)
            .Select(i => new DateOnly(2025, 1, 1).AddDays(i))
            .Select(d =>
            {
                var s = engine.Calculate(d, location, profile);
                return new ReferencePrayerDay(
                    d,
                    TimeOnly.FromTimeSpan(s.Fajr.TimeOfDay),
                    TimeOnly.FromTimeSpan(s.Sunrise.TimeOfDay),
                    TimeOnly.FromTimeSpan(s.Dhuhr.TimeOfDay),
                    TimeOnly.FromTimeSpan(s.Asr.TimeOfDay),
                    TimeOnly.FromTimeSpan(s.Maghrib.TimeOfDay),
                    TimeOnly.FromTimeSpan(s.Isha.TimeOfDay));
            })
            .ToList();

        var report = ScheduleComparer.Compare(
            days,
            d => engine.Calculate(d, location, profile));

        Assert.Equal(30 * 6, report.Deltas.Count);
        Assert.Equal(0, report.MaxAbsoluteMinutes);
        Assert.Equal(0, report.MeanAbsoluteMinutes);
    }

    [Fact]
    public void LoadCsv_ReadsFixtureFile()
    {
        var path = FindFixture("kennadsa-baseline-2025-01.csv");
        var days = ScheduleComparer.LoadCsv(path);
        Assert.True(days.Count >= 30);
        Assert.Equal(new DateOnly(2025, 1, 1), days[0].Date);
    }

    [Fact]
    public void BaselineFixture_MatchesEngineWithinOneMinute()
    {
        var path = FindFixture("kennadsa-baseline-2025-01.csv");
        var days = ScheduleComparer.LoadCsv(path);
        var engine = new PrayerEngine();

        var report = ScheduleComparer.Compare(
            days,
            d => engine.Calculate(d, KnownLocations.Kennadsa, CalculationMethods.Algeria));

        Assert.True(
            report.MaxAbsoluteMinutes <= 1,
            $"Max |Δ| was {report.MaxAbsoluteMinutes}. Report:\n{report.ToMarkdownSummary()}");
    }

    private static string FindFixture(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "testdata", "kennadsa", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find fixture {fileName}");
    }
}
