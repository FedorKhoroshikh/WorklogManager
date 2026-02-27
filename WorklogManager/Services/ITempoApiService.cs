using WorklogManager.Models;

namespace WorklogManager.Services;

public interface ITempoApiService
{
    /// <summary>
    /// Uploads a single worklog entry to the Tempo Cloud API v4.
    /// Retries up to 3 times on transient errors; respects 429 Retry-After headers.
    /// </summary>
    /// <returns>
    /// (Success: true, Error: null) on success,
    /// (Success: false, Error: message) on permanent failure.
    /// </returns>
    Task<(bool Success, string? Error)> UploadWorklogAsync(
        TempoWorklogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the Tempo connection by calling GET /4/worklogs?limit=1.
    /// </summary>
    Task<(bool Success, string? Error)> TestConnectionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all worklogs for the given user between <paramref name="from"/> and
    /// <paramref name="to"/> (inclusive) and returns a set of (date, issueKey) pairs.
    /// Handles pagination automatically.
    /// </summary>
    /// <returns>
    /// (Success: true, Existing: non-null set, Error: null) on success,
    /// (Success: false, Existing: null, Error: message) on failure.
    /// </returns>
    Task<(bool Success, HashSet<(string Date, string IssueKey)>? Existing, string? Error)>
        GetExistingWorklogsAsync(
            string authorAccountId,
            string from,
            string to,
            CancellationToken cancellationToken = default);
}
