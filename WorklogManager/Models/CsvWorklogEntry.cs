using System.Globalization;

namespace WorklogManager.Models;

/// <summary>
/// Represents a single raw row from the TogglTrack Detailed Report CSV.
/// Multiple entries with the same Start date + IssueKey are aggregated into one WorklogRecord.
/// </summary>
public class CsvWorklogEntry
{
    public string Description { get; set; } = string.Empty;
    public string StartDateRaw { get; set; } = string.Empty;   // "YYYY-MM-DD"
    public string StopDateRaw { get; set; } = string.Empty;    // "YYYY-MM-DD"
    public string Duration { get; set; } = string.Empty;        // "H:MM:SS"
    public string Project { get; set; } = string.Empty;         // "ISSUE-KEY   Full project title"
    public string Tags { get; set; } = string.Empty;
    public string StartTimeRaw { get; set; } = string.Empty;
    public string StopTimeRaw { get; set; } = string.Empty;

    /// <summary>Start date parsed from "YYYY-MM-DD".</summary>
    public DateTime StartDate =>
        DateTime.TryParseExact(StartDateRaw, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;

    /// <summary>Duration in seconds, parsed from "H:MM:SS".</summary>
    public int DurationSeconds
    {
        get
        {
            var parts = Duration.Split(':');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out int h) &&
                int.TryParse(parts[1], out int m) &&
                int.TryParse(parts[2], out int s))
            {
                return h * 3600 + m * 60 + s;
            }
            return 0;
        }
    }
}
