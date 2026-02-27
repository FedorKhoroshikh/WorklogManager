using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WorklogManager.Services;

/// <summary>
/// Validates Jira issue keys against the Jira REST API v3.
///
/// Features:
///   • Max 5 concurrent HTTP calls (SemaphoreSlim)
///   • In-memory cache to avoid redundant calls
///   • Also caches the numeric issue ID required by Tempo API v4
///   • Configures the shared HttpClient on each call based on current settings
/// </summary>
public class JiraValidationService : IJiraValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    private readonly SemaphoreSlim _semaphore = new(5, 5);
    // Cache: issueKey → (exists, numericId)
    private readonly Dictionary<string, (bool Exists, int? IssueId)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public JiraValidationService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<Dictionary<string, bool>> ValidateIssuesAsync(
        IEnumerable<string> issueKeys,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var uniqueKeys = issueKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        int completed = 0;

        ConfigureHttpClient();

        var tasks = uniqueKeys.Select(async key =>
        {
            // Return cached result without hitting the semaphore
            if (_cache.TryGetValue(key, out var cached))
            {
                lock (results) results[key] = cached.Exists;
                int c = Interlocked.Increment(ref completed);
                progress?.Report(c * 100 / uniqueKeys.Count);
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var (exists, issueId) = await FetchIssueAsync(key, cancellationToken);
                _cache[key] = (exists, issueId);
                lock (results) results[key] = exists;
            }
            finally
            {
                _semaphore.Release();
                int c = Interlocked.Increment(ref completed);
                progress?.Report(c * 100 / uniqueKeys.Count);
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    public async Task<int?> GetIssueIdAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(issueKey, out var cached))
            return cached.IssueId;

        ConfigureHttpClient();
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var (exists, issueId) = await FetchIssueAsync(issueKey, cancellationToken);
            _cache[issueKey] = (exists, issueId);
            return issueId;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string?> GetCurrentUserAccountIdAsync(CancellationToken cancellationToken = default)
    {
        ConfigureHttpClient();
        try
        {
            var response = await _httpClient.GetAsync("rest/api/3/myself", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("accountId", out var prop))
                return prop.GetString();
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    public void ClearCache() => _cache.Clear();

    // ── Private ──────────────────────────────────────────────────────────────

    private void ConfigureHttpClient()
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.JiraBaseUrl)) return;

        // BaseAddress can only be set before the first request; skip if already set.
        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri(settings.JiraBaseUrl.TrimEnd('/') + "/");

        var token = _settingsService.GetJiraApiToken();
        var credential = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{settings.JiraEmail}:{token}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credential);
    }

    /// <summary>
    /// Calls Jira REST API v3 for the given issue key.
    /// Returns (exists, numericId). The numeric ID is needed by Tempo API v4.
    /// </summary>
    private async Task<(bool Exists, int? IssueId)> FetchIssueAsync(string issueKey, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"rest/api/3/issue/{issueKey}?fields=summary", ct);

            if (response.StatusCode != HttpStatusCode.OK)
                return (false, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            int? issueId = null;
            if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                int.TryParse(idProp.GetString(), out int parsed))
            {
                issueId = parsed;
            }
            return (true, issueId);
        }
        catch (OperationCanceledException) { throw; }
        catch { return (false, null); }
    }
}
