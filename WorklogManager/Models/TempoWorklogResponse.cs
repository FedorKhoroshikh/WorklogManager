using System.Text.Json.Serialization;

namespace WorklogManager.Models;

public class TempoWorklogResponse
{
    [JsonPropertyName("tempoWorklogId")]
    public long? TempoWorklogId { get; set; }

    [JsonPropertyName("issueKey")]
    public string? IssueKey { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }
}

public class TempoErrorResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errors")]
    public List<TempoFieldError>? Errors { get; set; }
}

public class TempoFieldError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Paginated response from GET /4/worklogs/user/{accountId}.
/// Used to detect worklogs that already exist in Tempo before uploading.
/// </summary>
public class TempoWorklogsPage
{
    [JsonPropertyName("metadata")]
    public TempoWorklogsMetadata? Metadata { get; set; }

    [JsonPropertyName("results")]
    public List<TempoWorklogEntry>? Results { get; set; }
}

public class TempoWorklogsMetadata
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>URL of the next page, or null if this is the last page.</summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

public class TempoWorklogEntry
{
    [JsonPropertyName("tempoWorklogId")]
    public long TempoWorklogId { get; set; }

    [JsonPropertyName("issue")]
    public TempoIssueRef? Issue { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }
}

public class TempoIssueRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }
}
