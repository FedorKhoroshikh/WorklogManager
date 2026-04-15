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
    /// Rounds a "HH:mm:ss" start-time string to the nearest 5-minute boundary.
    /// Rules (based on last digit of minutes):
    ///   3,4,5,6,7 → round to 5   (e.g. 10:33 → 10:35, 10:17 → 10:15)
    ///   0,1,2,8,9 → round to 0   (e.g. 10:51 → 10:50, 10:48 → 10:50, 10:59 → 11:00)
    /// Seconds are always zeroed out.
    /// </summary>
    public static string RoundStartTimeTo5Min(string startTime)
    {
        if (string.IsNullOrWhiteSpace(startTime)) return startTime;

        // Parse hours and minutes from "HH:mm:ss" or "HH:mm"
        var parts = startTime.Split(':');
        if (parts.Length < 2) return startTime;

        if (!int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes))
            return startTime;

        int lastDigit = minutes % 10;
        int roundedMinutes;

        if (lastDigit is >= 3 and <= 7)
        {
            // Round to nearest 5: replace last digit with 5
            roundedMinutes = (minutes / 10) * 10 + 5;
        }
        else
        {
            // Round to nearest 0: 0,1,2 → down to 0; 8,9 → up to next 0
            if (lastDigit <= 2)
                roundedMinutes = (minutes / 10) * 10;
            else // 8 or 9
                roundedMinutes = (minutes / 10) * 10 + 10;
        }

        // Handle minute overflow
        if (roundedMinutes >= 60)
        {
            roundedMinutes -= 60;
            hours++;
            if (hours >= 24) hours = 0;
        }

        return $"{hours:D2}:{roundedMinutes:D2}:00";
    }

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
