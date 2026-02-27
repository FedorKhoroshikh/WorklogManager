using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WorklogManager.Models;

namespace WorklogManager.Services;

/// <summary>
/// Uploads worklog entries to the Tempo Cloud API v4.
///
/// Features:
///   • POST https://api.tempo.io/4/worklogs
///   • Bearer token authentication
///   • Retry logic: up to 3 attempts with exponential back-off
///   • Respects 429 Retry-After header
///   • Hard failures on 400/401/403/404 (no retry)
/// </summary>
public class TempoApiService : ITempoApiService
{
    private const string BaseUrl = "https://api.tempo.io/";
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TempoApiService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string? Error)> UploadWorklogAsync(
        TempoWorklogRequest request,
        CancellationToken cancellationToken = default)
    {
        ConfigureAuth();

        var json = JsonSerializer.Serialize(request, JsonOptions);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("4/worklogs", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return (true, null);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                // Rate-limited: wait and retry
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                // Hard failures — no retry
                if (response.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.NotFound)
                {
                    return (false, $"HTTP {(int)response.StatusCode}: {ParseErrorMessage(body)}");
                }

                // Other server errors — retry with back-off
                if (attempt < MaxRetries - 1)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries - 1)
                    return (false, ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        return (false, "Max retries exceeded");
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        try
        {
            var response = await _httpClient.GetAsync("4/worklogs?limit=1", cancellationToken);
            return response.IsSuccessStatusCode
                ? (true, null)
                : (false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, HashSet<(string Date, string IssueKey)>? Existing, string? Error)>
        GetExistingWorklogsAsync(
            string authorAccountId,
            string from,
            string to,
            CancellationToken cancellationToken = default)
    {
        ConfigureAuth();

        var existing = new HashSet<(string, string)>();
        const int limit = 1000;
        int offset = 0;

        while (true)
        {
            var url = $"4/worklogs/user/{Uri.EscapeDataString(authorAccountId)}" +
                      $"?from={from}&to={to}&limit={limit}&offset={offset}";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return (false, null, $"HTTP {(int)response.StatusCode}: {ParseErrorMessage(body)}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var page = JsonSerializer.Deserialize<TempoWorklogsPage>(json, JsonOptions);

                if (page?.Results == null || page.Results.Count == 0)
                    break;

                foreach (var entry in page.Results)
                {
                    if (entry.StartDate != null && entry.Issue?.Key != null)
                        existing.Add((entry.StartDate, entry.Issue.Key));
                }

                // No next page link or fewer results than requested → done
                if (page.Metadata?.Next == null || page.Results.Count < limit)
                    break;

                offset += limit;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        return (true, existing, null);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void ConfigureAuth()
    {
        var token = _settingsService.GetTempoApiToken();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static string ParseErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Unknown error";
        try
        {
            var err = JsonSerializer.Deserialize<TempoErrorResponse>(body, JsonOptions);
            if (err?.Errors?.Count > 0)
                return string.Join("; ", err.Errors.Select(e => e.Message));
            return err?.Message ?? body;
        }
        catch
        {
            return body.Length > 200 ? body[..200] : body;
        }
    }
}
