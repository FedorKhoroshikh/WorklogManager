using System.Text.Json.Serialization;

namespace WorklogManager.Models;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Request body for POST /reports/api/v3/workspace/{id}/search/time_entries
/// </summary>
internal class TogglReportRequest
{
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("grouped")]
    public bool Grouped { get; set; } = false;

    [JsonPropertyName("enrich_response")]
    public bool EnrichResponse { get; set; } = true;

    [JsonPropertyName("order_by")]
    public string OrderBy { get; set; } = "date";

    [JsonPropertyName("order_dir")]
    public string OrderDir { get; set; } = "ASC";

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; } = 50;

    [JsonPropertyName("first_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FirstId { get; set; }

    [JsonPropertyName("first_row_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FirstRowNumber { get; set; }

    [JsonPropertyName("first_timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FirstTimestamp { get; set; }
}

// ── Response ──────────────────────────────────────────────────────────────────

/// <summary>
/// One row in the response array. With grouped=false each row wraps a single time entry.
/// With enrich_response=true the project_name field is populated.
/// </summary>
internal class TogglReportEntry
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("project_id")]
    public long? ProjectId { get; set; }

    [JsonPropertyName("project_name")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("row_number")]
    public long RowNumber { get; set; }

    [JsonPropertyName("time_entries")]
    public List<TogglReportTimeEntry> TimeEntries { get; set; } = new();
}

internal class TogglReportTimeEntry
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Duration in seconds (always positive, unlike the core /me/time_entries endpoint).</summary>
    [JsonPropertyName("seconds")]
    public int Seconds { get; set; }

    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("stop")]
    public string? Stop { get; set; }
}

// ── /me response ──────────────────────────────────────────────────────────────

/// <summary>Partial response from GET /api/v9/me — only fields we need.</summary>
internal class TogglMeResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("fullname")]
    public string Fullname { get; set; } = string.Empty;

    [JsonPropertyName("default_workspace_id")]
    public long DefaultWorkspaceId { get; set; }
}
