using System.Text.Json.Serialization;

namespace WorklogManager.Models;

/// <summary>
/// Payload for POST https://api.tempo.io/4/worklogs
/// Required by Tempo API v4: issueId, authorAccountId, startDate, timeSpentSeconds.
/// </summary>
public class TempoWorklogRequest
{
    /// <summary>Numeric Jira issue ID (required by Tempo API v4).</summary>
    [JsonPropertyName("issueId")]
    public int IssueId { get; set; }

    /// <summary>Jira account ID of the worklog author (required by Tempo API v4).</summary>
    [JsonPropertyName("authorAccountId")]
    public string AuthorAccountId { get; set; } = string.Empty;

    [JsonPropertyName("issueKey")]
    public string IssueKey { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = string.Empty;   // "YYYY-MM-DD"

    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional start time in "HH:mm:ss" format.
    /// Null when not available (omitted from the JSON payload).
    /// </summary>
    [JsonPropertyName("startTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; set; }

    public static TempoWorklogRequest FromRecord(WorklogRecord record, string authorAccountId) => new()
    {
        IssueId = record.JiraIssueId ?? 0,
        AuthorAccountId = authorAccountId,
        IssueKey = record.IssueKey,
        StartDate = record.Date.ToString("yyyy-MM-dd"),
        TimeSpentSeconds = record.RoundedTimeSpentSeconds,
        Description = record.Description,
        StartTime = string.IsNullOrEmpty(record.StartTime) ? null : record.StartTime
    };
}
