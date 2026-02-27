namespace WorklogManager.Services;

public interface IJiraValidationService
{
    /// <summary>
    /// Validates whether each unique Jira issue key exists via the Jira REST API.
    /// Uses a SemaphoreSlim(5) to limit concurrency and caches results.
    /// Also caches the numeric Jira issue ID for use in Tempo uploads.
    /// </summary>
    Task<Dictionary<string, bool>> ValidateIssuesAsync(
        IEnumerable<string> issueKeys,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the numeric Jira issue ID for the given key (from cache or API).
    /// Returns null if the issue does not exist or Jira is unreachable.
    /// </summary>
    Task<int?> GetIssueIdAsync(string issueKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the Jira account ID of the currently authenticated user
    /// via GET /rest/api/3/myself.
    /// </summary>
    Task<string?> GetCurrentUserAccountIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the validation result cache.</summary>
    void ClearCache();
}
