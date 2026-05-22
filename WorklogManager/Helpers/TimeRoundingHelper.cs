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
    /// Sub-3-minute entries collapse to 0 (no longer extended to 5 min).
    /// </summary>
    public static int RoundToNearest5Min(int seconds)
    {
        int totalMinutes = seconds / OneMinuteInSeconds;
        int remainder = totalMinutes % 5;

        int roundedMinutes = remainder < 3
            ? totalMinutes - remainder          // round down  (0,1,2 → subtract)
            : totalMinutes + (5 - remainder);   // round up    (3,4   → add)

        return Math.Max(0, roundedMinutes) * OneMinuteInSeconds;
    }

    /// <summary>
    /// Rounds each entry's start and end times independently to the nearest 5-minute
    /// boundary; the new duration is (roundedEnd - roundedStart), floored at 0.
    /// Entries shorter than 3 minutes collapse to 0-duration (must be reviewed by user).
    /// Overlaps are NOT resolved here — they're detected separately and flagged in the UI.
    /// </summary>
    public static void RoundTimestampsAndDurations(IList<WorklogRecord> records)
    {
        foreach (var r in records)
        {
            int? startSec = StartTimeToSeconds(r.StartTime);
            if (startSec == null)
            {
                // No parseable start time → fall back to duration-only rounding.
                r.RoundedTimeSpentSeconds = RoundToNearest5Min(r.RoundedTimeSpentSeconds);
                continue;
            }

            int endSec = startSec.Value + r.RoundedTimeSpentSeconds;

            string roundedStart = RoundStartTimeTo5Min(SecondsToTimeString(startSec.Value));
            string roundedEnd   = RoundStartTimeTo5Min(SecondsToTimeString(endSec));

            int newStartSec = StartTimeToSeconds(roundedStart)!.Value;
            int newEndSec   = StartTimeToSeconds(roundedEnd)!.Value;

            r.StartTime = roundedStart;
            r.RoundedTimeSpentSeconds = Math.Max(0, newEndSec - newStartSec);
        }
    }

    /// <summary>
    /// True when (startTime + durationSeconds) collapses to a zero-length interval
    /// after rounding both endpoints to the nearest 5 minutes — i.e. the entry
    /// will disappear when "Round all to 5 min" is applied.
    /// Returns true for already-zero or sub-3-min durations regardless of start time.
    /// </summary>
    public static bool WouldRoundToZero(string startTime, int durationSeconds)
    {
        if (durationSeconds <= 0) return true;

        int? startSec = StartTimeToSeconds(startTime);
        if (startSec == null)
        {
            // No start time — only the duration matters; reuse the duration rounder.
            return RoundToNearest5Min(durationSeconds) == 0;
        }

        int endSec = startSec.Value + durationSeconds;
        int roundedStart = StartTimeToSeconds(RoundStartTimeTo5Min(SecondsToTimeString(startSec.Value)))!.Value;
        int roundedEnd   = StartTimeToSeconds(RoundStartTimeTo5Min(SecondsToTimeString(endSec)))!.Value;
        return roundedEnd <= roundedStart;
    }

    /// <summary>Formats total seconds since midnight as "HH:mm:ss" (wraps modulo 24h).</summary>
    private static string SecondsToTimeString(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int h = (totalSeconds / 3600) % 24;
        int m = (totalSeconds / 60) % 60;
        int s = totalSeconds % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    /// <summary>
    /// Parses a "HH:mm:ss" or "HH:mm" string into total seconds since midnight.
    /// Returns null if parsing fails or the string is empty.
    /// </summary>
    private static int? StartTimeToSeconds(string startTime)
    {
        if (string.IsNullOrWhiteSpace(startTime)) return null;
        var parts = startTime.Split(':');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return null;
        int s = parts.Length >= 3 && int.TryParse(parts[2], out int sec) ? sec : 0;
        return h * 3600 + m * 60 + s;
    }

    /// <summary>
    /// Parses a "HH:mm:ss" or "HH:mm" string into total minutes since midnight.
    /// Returns null if parsing fails or the string is empty.
    /// </summary>
    public static int? StartTimeToMinutes(string startTime)
    {
        if (string.IsNullOrWhiteSpace(startTime)) return null;
        var parts = startTime.Split(':');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return null;
        return h * 60 + m;
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
