using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using WorklogManager.Helpers;
using WorklogManager.Models;

namespace WorklogManager.Services;

/// <summary>
/// Parses TogglTrack Detailed Report CSV files.
///
/// CSV format:
///   "Description","Start date","Stop date","Duration","Project","Tags","Start time","Stop time"
///
/// Returns one WorklogRecord per non-zero-duration CSV row (no aggregation).
/// Aggregation and rounding are applied in MainViewModel based on the MergeRecords option.
/// </summary>
public class CsvParserService : ICsvParserService
{
    private static readonly Regex ProjectRegex =
        new(@"^([A-Z]+-\d+)\s+(.*)", RegexOptions.Compiled);

    /// <summary>
    /// Matches a known Jira issue key prefix at the start of the Description field.
    /// Supported prefixes: WT, CSD, CSM, TIP.
    /// </summary>
    private static readonly Regex PapasModeDescriptionRegex =
        new(@"^((?:WT|CSD|CSM|TIP)-\d+)\s*(.*)", RegexOptions.Compiled);

    private static readonly Regex ValidIssueKey =
        new(@"^[A-Z]+-\d+$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<WorklogRecord>> ParseAsync(string filePath, bool papasMode = false)
    {
        var entries = await Task.Run(() => ReadCsvEntries(filePath));
        return papasMode ? ParseToRawRecordsPapasMode(entries) : ParseToRawRecords(entries);
    }

    // ── CSV reading ──────────────────────────────────────────────────────────

    private static List<RawEntry> ReadCsvEntries(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(filePath);
        // CsvHelper 30+ accepts CsvConfiguration via the IReaderConfiguration overload
        using var csv = new CsvReader(reader, (IReaderConfiguration)config);
        csv.Context.RegisterClassMap<RawEntryMap>();
        return csv.GetRecords<RawEntry>().ToList();
    }

    // ── Raw record parsing (no aggregation) ─────────────────────────────────

    private static List<WorklogRecord> ParseToRawRecords(List<RawEntry> entries)
    {
        return entries
            .Where(e => e.DurationSeconds > 0 && e.StartDate != DateTime.MinValue)
            .Select(e => new
            {
                Entry = e,
                IssueKey = ExtractIssueKey(e.Project),
                ProjectName = ExtractProjectDescription(e.Project)
            })
            .Where(x => ValidIssueKey.IsMatch(x.IssueKey))
            .Select(x => new WorklogRecord
            {
                Date = x.Entry.StartDate,
                IssueKey = x.IssueKey,
                ProjectName = x.ProjectName,
                Description = x.Entry.Description.Trim(),      // actual activity description
                OriginalTimeSpentSeconds = x.Entry.DurationSeconds,
                RoundedTimeSpentSeconds = x.Entry.DurationSeconds,  // rounding done in ViewModel
                IsSelected = true,
                UploadStatus = "Pending"
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.IssueKey)
            .ToList();
    }

    // ── PapasMode parsing (issue key from Description) ───────────────────────

    /// <summary>
    /// PapasMode variant: the Jira issue key is the first token of the Description field
    /// (e.g. "WT-13065 tuning local environment"). The remainder becomes the worklog comment.
    /// </summary>
    private static List<WorklogRecord> ParseToRawRecordsPapasMode(List<RawEntry> entries)
    {
        return entries
            .Where(e => e.DurationSeconds > 0 && e.StartDate != DateTime.MinValue)
            .Select(e => new
            {
                Entry = e,
                Match = PapasModeDescriptionRegex.Match(e.Description.Trim())
            })
            .Where(x => x.Match.Success)
            .Select(x => new WorklogRecord
            {
                Date = x.Entry.StartDate,
                IssueKey = x.Match.Groups[1].Value,
                ProjectName = x.Entry.Project.Trim(),
                Description = x.Match.Groups[2].Value.Trim(),
                OriginalTimeSpentSeconds = x.Entry.DurationSeconds,
                RoundedTimeSpentSeconds = x.Entry.DurationSeconds,
                IsSelected = true,
                UploadStatus = "Pending"
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.IssueKey)
            .ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ExtractIssueKey(string project)
    {
        if (string.IsNullOrWhiteSpace(project)) return string.Empty;
        var match = ProjectRegex.Match(project.Trim());
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ExtractProjectDescription(string project)
    {
        if (string.IsNullOrWhiteSpace(project)) return string.Empty;
        var match = ProjectRegex.Match(project.Trim());
        return match.Success ? match.Groups[2].Value.Trim() : project.Trim();
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private class RawEntry
    {
        public string Description { get; set; } = string.Empty;
        public string StartDateRaw { get; set; } = string.Empty;
        public string StopDateRaw { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string StartTimeRaw { get; set; } = string.Empty;
        public string StopTimeRaw { get; set; } = string.Empty;

        public DateTime StartDate =>
            DateTime.TryParseExact(StartDateRaw, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : DateTime.MinValue;

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

    private sealed class RawEntryMap : ClassMap<RawEntry>
    {
        public RawEntryMap()
        {
            Map(m => m.Description).Name("Description");
            Map(m => m.StartDateRaw).Name("Start date");
            Map(m => m.StopDateRaw).Name("Stop date");
            Map(m => m.Duration).Name("Duration");
            Map(m => m.Project).Name("Project");
            Map(m => m.Tags).Name("Tags");
            Map(m => m.StartTimeRaw).Name("Start time");
            Map(m => m.StopTimeRaw).Name("Stop time");
        }
    }
}
