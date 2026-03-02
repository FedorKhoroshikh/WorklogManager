using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorklogManager.Models;

namespace WorklogManager.Services;

/// <summary>
/// Fetches time entries from the TogglTrack Reports API v3.
///
/// Flow:
///   GET  {baseUrl}/api/v9/me                                              → resolve default_workspace_id
///   POST {baseUrl}/reports/api/v3/workspace/{id}/search/time_entries      → paginated entries
///
/// The Jira issue key is extracted from the description field using the same
/// known-prefix regex as PapasMode CSV parsing (WT, CSD, CSM, TIP).
/// Auth: HTTP Basic with "{api_token}:api_token".
/// </summary>
public class TogglTrackApiTimeEntryProvider : ITimeEntryProvider
{
    private static readonly Regex IssueKeyRegex =
        new(@"^((?:WT|CSD|CSM|TIP)-\d+)\s*(.*)", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    public TogglTrackApiTimeEntryProvider(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<WorklogRecord>> GetTimeRecordsAsync(
        TimeEntryProviderContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.DateFrom is null || context.DateTo is null)
            throw new ArgumentException("DateFrom and DateTo must be set for TogglTrack API import.", nameof(context));

        var settings = _settingsService.Load();
        var token = _settingsService.GetTogglTrackApiToken();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("TogglTrack API token is not configured.");

        var baseUrl = settings.TogglTrackBaseUrl.TrimEnd('/');
        ConfigureAuth(token);

        var workspaceId = await GetWorkspaceIdAsync(baseUrl, cancellationToken);

        var results = new List<WorklogRecord>();
        var request = new TogglReportRequest
        {
            StartDate = context.DateFrom.Value.ToString("yyyy-MM-dd"),
            EndDate   = context.DateTo.Value.ToString("yyyy-MM-dd"),
            PageSize  = 50
        };

        var url = $"{baseUrl}/reports/api/v3/workspace/{workspaceId}/search/time_entries";

        while (true)
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var entries = JsonSerializer.Deserialize<List<TogglReportEntry>>(body, JsonOptions)
                          ?? new List<TogglReportEntry>();

            foreach (var entry in entries)
                MapEntry(results, entry);

            // Cursor-based pagination via response headers
            if (!response.Headers.TryGetValues("X-Next-ID", out var nextIdValues))
                break;

            var nextIdStr = nextIdValues.FirstOrDefault();
            if (string.IsNullOrEmpty(nextIdStr) || !long.TryParse(nextIdStr, out var nextId))
                break;

            response.Headers.TryGetValues("X-Next-Row-Number", out var nextRowValues);
            long.TryParse(nextRowValues?.FirstOrDefault(), out var nextRow);

            // first_timestamp = unix start of the last entry on the current page
            long? firstTimestamp = null;
            var last = entries.LastOrDefault();
            if (last?.TimeEntries.Count > 0 &&
                DateTimeOffset.TryParse(last.TimeEntries[0].Start, out var lastDto))
            {
                firstTimestamp = lastDto.ToUnixTimeSeconds();
            }

            request.FirstId        = nextId;
            request.FirstRowNumber = nextRow > 0 ? nextRow : null;
            request.FirstTimestamp = firstTimestamp;
        }

        return results
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StartTime)
            .ThenBy(r => r.IssueKey)
            .ToList();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void MapEntry(List<WorklogRecord> results, TogglReportEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Description)) return;
        if (entry.TimeEntries.Count == 0) return;

        var match = IssueKeyRegex.Match(entry.Description.Trim());
        if (!match.Success) return;

        foreach (var te in entry.TimeEntries)
        {
            if (te.Seconds <= 0) continue;

            DateTime date = DateTime.MinValue;
            string startTime = string.Empty;

            if (!string.IsNullOrEmpty(te.Start) &&
                DateTimeOffset.TryParse(te.Start, out var dto))
            {
                var local = dto.LocalDateTime;
                date      = local.Date;
                startTime = local.ToString("HH:mm:ss");
            }

            if (date == DateTime.MinValue) continue;

            results.Add(new WorklogRecord
            {
                Date        = date,
                IssueKey    = match.Groups[1].Value,
                ProjectName = entry.ProjectName ?? string.Empty,
                Description = match.Groups[2].Value.Trim(),
                StartTime   = startTime,
                OriginalTimeSpentSeconds = te.Seconds,
                RoundedTimeSpentSeconds  = te.Seconds,
                IsSelected   = true,
                UploadStatus = "Pending"
            });
        }
    }

    private async Task<long> GetWorkspaceIdAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"{baseUrl}/api/v9/me", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var me = JsonSerializer.Deserialize<TogglMeResponse>(body, JsonOptions);
        return me?.DefaultWorkspaceId
               ?? throw new InvalidOperationException("Could not retrieve workspace ID from TogglTrack.");
    }

    private void ConfigureAuth(string token)
    {
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{token}:api_token"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credential);
    }
}
