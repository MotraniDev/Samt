using System.Globalization;
using System.Text;
using Samt.Core.Domain;

namespace Samt.Core.Calculation;

public sealed record ReferencePrayerDay(
    DateOnly Date,
    TimeOnly Fajr,
    TimeOnly Sunrise,
    TimeOnly Dhuhr,
    TimeOnly Asr,
    TimeOnly Maghrib,
    TimeOnly Isha);

public sealed record PrayerDelta(
    DateOnly Date,
    PrayerEvent Event,
    TimeOnly Expected,
    TimeOnly Actual,
    int DeltaMinutes);

public sealed class ComparisonReport
{
    public required IReadOnlyList<PrayerDelta> Deltas { get; init; }

    public double MeanAbsoluteMinutes =>
        Deltas.Count == 0 ? 0 : Deltas.Average(d => Math.Abs(d.DeltaMinutes));

    public int MaxAbsoluteMinutes =>
        Deltas.Count == 0 ? 0 : Deltas.Max(d => Math.Abs(d.DeltaMinutes));

    public IReadOnlyDictionary<PrayerEvent, double> MeanByEvent =>
        Deltas
            .GroupBy(d => d.Event)
            .ToDictionary(g => g.Key, g => g.Average(x => (double)x.DeltaMinutes));

    public string ToMarkdownSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Event | Mean Δ (min) |");
        sb.AppendLine("|---|---:|");
        foreach (var (evt, mean) in MeanByEvent.OrderBy(kv => kv.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {evt} | {mean:0.00} |");
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Mean |Δ|: {MeanAbsoluteMinutes:0.00} min");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Max |Δ|: {MaxAbsoluteMinutes} min");
        return sb.ToString();
    }
}

public static class ScheduleComparer
{
    private static readonly PrayerEvent[] ComparedEvents =
    [
        PrayerEvent.Fajr,
        PrayerEvent.Sunrise,
        PrayerEvent.Dhuhr,
        PrayerEvent.Asr,
        PrayerEvent.Maghrib,
        PrayerEvent.Isha
    ];

    public static ComparisonReport Compare(
        IEnumerable<ReferencePrayerDay> referenceDays,
        Func<DateOnly, PrayerSchedule> calculate)
    {
        var deltas = new List<PrayerDelta>();

        foreach (var day in referenceDays)
        {
            var schedule = calculate(day.Date);
            foreach (var evt in ComparedEvents)
            {
                var expected = GetExpected(day, evt);
                var actual = TimeOnly.FromTimeSpan(schedule.Times[evt].TimeOfDay);
                var delta = (int)Math.Round((actual.ToTimeSpan() - expected.ToTimeSpan()).TotalMinutes);
                deltas.Add(new PrayerDelta(day.Date, evt, expected, actual, delta));
            }
        }

        return new ComparisonReport { Deltas = deltas };
    }

    public static IReadOnlyList<ReferencePrayerDay> LoadCsv(string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToList();

        if (lines.Count == 0)
        {
            return [];
        }

        // Skip header if present
        var start = lines[0].Contains("date", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var result = new List<ReferencePrayerDay>();

        for (var i = start; i < lines.Count; i++)
        {
            var parts = lines[i].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
            {
                throw new InvalidDataException($"Invalid reference row: {lines[i]}");
            }

            result.Add(new ReferencePrayerDay(
                DateOnly.Parse(parts[0], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[1], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[2], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[3], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[4], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[5], CultureInfo.InvariantCulture),
                TimeOnly.Parse(parts[6], CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static TimeOnly GetExpected(ReferencePrayerDay day, PrayerEvent evt) => evt switch
    {
        PrayerEvent.Fajr => day.Fajr,
        PrayerEvent.Sunrise => day.Sunrise,
        PrayerEvent.Dhuhr => day.Dhuhr,
        PrayerEvent.Asr => day.Asr,
        PrayerEvent.Maghrib => day.Maghrib,
        PrayerEvent.Isha => day.Isha,
        _ => throw new ArgumentOutOfRangeException(nameof(evt))
    };
}
