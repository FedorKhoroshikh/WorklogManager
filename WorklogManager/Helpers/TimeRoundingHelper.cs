using WorklogManager.Models;

namespace WorklogManager.Helpers;

/// <summary>
/// Handles rounding worklog durations to 5-minute granularity for Tempo time sheets.
///
/// Rounding rules:
///   • Each entry is rounded independently to the nearest 5 minutes.
///   • Per-day invariant: the sum of rounded entries MUST NOT be less than
///     the original day total. If individual roundings cause a deficit, the
///     record with the largest downward rounding receives a compensating +5 min.
///
/// Example:
///   Entries: 27 min, 33 min  →  rounded: 25 min, 35 min  →  sum 60 min == original ✔
///   Entries: 22 min, 22 min, 22 min  →  rounded: 20+20+20=60 min < 66 min  →  compensate:
///     all three were rounded down by 2 each (tie), pick first → 25+20+20=65 min ≥ 66? No.
///     Repeat: 25+25+20=70 min ≥ 66 min ✔
/// </summary>
public static class TimeRoundingHelper
{
    private const int FiveMinutesInSeconds = 300;
    private const int OneMinuteInSeconds = 60;

    /// <summary>
    /// Rounds a duration (in seconds) to the nearest 5-minute boundary.
    /// The minimum result is 5 minutes (300 s) — zero-duration entries become 5 min.
    /// </summary>
    public static int RoundToNearest5Min(int seconds)
    {
        int totalMinutes = seconds / OneMinuteInSeconds;
        int remainder = totalMinutes % 5;

        int roundedMinutes = remainder < 3
            ? totalMinutes - remainder          // round down  (0,1,2 → subtract)
            : totalMinutes + (5 - remainder);   // round up    (3,4   → add)

        return Math.Max(5, roundedMinutes) * OneMinuteInSeconds;
    }

    /// <summary>
    /// Applies per-record 5-minute rounding to all records belonging to one day,
    /// then compensates any deficit so the day total does not decrease.
    /// </summary>
    public static void ApplyDayRounding(IList<WorklogRecord> dayRecords)
    {
        if (dayRecords.Count == 0) return;

        // Step 1: round each record independently
        int originalTotal = dayRecords.Sum(r => r.OriginalTimeSpentSeconds);
        foreach (var r in dayRecords)
            r.RoundedTimeSpentSeconds = RoundToNearest5Min(r.OriginalTimeSpentSeconds);

        // Step 2: compensate for total that dropped below original
        int roundedTotal = dayRecords.Sum(r => r.RoundedTimeSpentSeconds);

        while (roundedTotal < originalTotal)
        {
            // Find the record that was rounded down the most (biggest time loss).
            // If all were rounded up, pick the largest record instead.
            var candidate = dayRecords
                .Where(r => r.OriginalTimeSpentSeconds > r.RoundedTimeSpentSeconds)
                .OrderByDescending(r => r.OriginalTimeSpentSeconds - r.RoundedTimeSpentSeconds)
                .FirstOrDefault()
                ?? dayRecords.OrderByDescending(r => r.RoundedTimeSpentSeconds).First();

            candidate.RoundedTimeSpentSeconds += FiveMinutesInSeconds;
            roundedTotal += FiveMinutesInSeconds;
        }
    }
}
