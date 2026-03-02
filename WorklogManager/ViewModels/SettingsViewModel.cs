using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WorklogManager.Helpers;
using WorklogManager.Services;

namespace WorklogManager.ViewModels;

/// <summary>
/// ViewModel for the Settings window.
/// Handles loading, saving, and testing Jira/Tempo credentials.
/// Plain-text tokens exist only in memory; they are encrypted before being saved.
/// </summary>
public class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ITempoApiService _tempoApi;

    public SettingsViewModel(ISettingsService settingsService, ITempoApiService tempoApi)
    {
        _settingsService = settingsService;
        _tempoApi = tempoApi;
        LoadFromSettings();

        TestJiraCommand       = new AsyncRelayCommand(TestJiraAsync);
        TestTempoCommand      = new AsyncRelayCommand(TestTempoAsync);
        TestTogglTrackCommand = new AsyncRelayCommand(TestTogglTrackAsync);
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrWhiteSpace(JiraBaseUrl));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    private string _jiraEmail = string.Empty;
    public string JiraEmail
    {
        get => _jiraEmail;
        set => SetProperty(ref _jiraEmail, value);
    }

    private string _jiraApiToken = string.Empty;
    public string JiraApiToken
    {
        get => _jiraApiToken;
        set => SetProperty(ref _jiraApiToken, value);
    }

    private string _tempoApiToken = string.Empty;
    public string TempoApiToken
    {
        get => _tempoApiToken;
        set => SetProperty(ref _tempoApiToken, value);
    }

    private string _jiraBaseUrl = string.Empty;
    public string JiraBaseUrl
    {
        get => _jiraBaseUrl;
        set => SetProperty(ref _jiraBaseUrl, value);
    }

    private bool _papasMode;
    /// <summary>
    /// When true, the Jira issue key is read from the Description column of the CSV
    /// instead of the Project column.
    /// </summary>
    public bool PapasMode
    {
        get => _papasMode;
        set => SetProperty(ref _papasMode, value);
    }

    private string _testStatus = string.Empty;
    public string TestStatus
    {
        get => _testStatus;
        set => SetProperty(ref _testStatus, value);
    }

    private string _togglTrackEmail = string.Empty;
    public string TogglTrackEmail
    {
        get => _togglTrackEmail;
        set => SetProperty(ref _togglTrackEmail, value);
    }

    private string _togglTrackApiToken = string.Empty;
    public string TogglTrackApiToken
    {
        get => _togglTrackApiToken;
        set => SetProperty(ref _togglTrackApiToken, value);
    }

    private string _togglTrackBaseUrl = "https://api.track.toggl.com";
    public string TogglTrackBaseUrl
    {
        get => _togglTrackBaseUrl;
        set => SetProperty(ref _togglTrackBaseUrl, value);
    }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public AsyncRelayCommand TestJiraCommand { get; }
    public AsyncRelayCommand TestTempoCommand { get; }
    public AsyncRelayCommand TestTogglTrackCommand { get; }
    public RelayCommand SaveCommand { get; }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void Save()
    {
        var settings = _settingsService.Load();
        settings.JiraEmail = JiraEmail.Trim();
        settings.JiraBaseUrl = JiraBaseUrl.Trim().TrimEnd('/');

        if (!string.IsNullOrEmpty(JiraApiToken))
            settings.JiraApiTokenEncrypted = CredentialHelper.Encrypt(JiraApiToken);

        if (!string.IsNullOrEmpty(TempoApiToken))
            settings.TempoApiTokenEncrypted = CredentialHelper.Encrypt(TempoApiToken);

        settings.PapasMode = PapasMode;

        settings.TogglTrackEmail   = TogglTrackEmail.Trim();
        settings.TogglTrackBaseUrl = string.IsNullOrWhiteSpace(TogglTrackBaseUrl)
            ? "https://api.track.toggl.com"
            : TogglTrackBaseUrl.Trim().TrimEnd('/');

        if (!string.IsNullOrEmpty(TogglTrackApiToken))
            settings.TogglTrackApiTokenEncrypted = CredentialHelper.Encrypt(TogglTrackApiToken);

        _settingsService.Save(settings);
        TestStatus = "Settings saved.";
    }

    // ── Test Jira ─────────────────────────────────────────────────────────────

    private async Task TestJiraAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(JiraBaseUrl) || string.IsNullOrWhiteSpace(JiraEmail))
        {
            TestStatus = "Jira URL and Email are required.";
            return;
        }

        IsTesting = true;
        TestStatus = "Testing Jira connection…";

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(JiraBaseUrl.TrimEnd('/') + "/");

            var token = string.IsNullOrEmpty(JiraApiToken)
                ? _settingsService.GetJiraApiToken()
                : JiraApiToken;

            // Trim to avoid invisible whitespace from copy-paste
            var email = JiraEmail.Trim();
            var trimmedToken = token.Trim();

            var credential = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{email}:{trimmedToken}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credential);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync("rest/api/3/myself", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                TestStatus = $"Jira OK — connected as {email}";
            }
            else
            {
                // Show HTTP code + response body for diagnosis
                var shortBody = body.Length > 400 ? body[..400] + "…" : body;
                TestStatus = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                             $"URL: {client.BaseAddress}rest/api/3/myself\n" +
                             $"Email: {email}\n" +
                             $"Token length: {trimmedToken.Length} chars\n\n" +
                             $"Response body:\n{shortBody}";
            }
        }
        catch (Exception ex)
        {
            TestStatus = $"Jira error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ── Test Tempo ────────────────────────────────────────────────────────────

    private async Task TestTempoAsync(CancellationToken ct)
    {
        IsTesting = true;
        TestStatus = "Testing Tempo connection…";

        try
        {
            var token = string.IsNullOrEmpty(TempoApiToken)
                ? _settingsService.GetTempoApiToken()
                : TempoApiToken;

            if (string.IsNullOrEmpty(token))
            {
                TestStatus = "Tempo API token is empty.";
                return;
            }

            var (success, error) = await _tempoApi.TestConnectionAsync(token, ct);
            TestStatus = success
                ? "Tempo OK — connection successful"
                : $"Tempo failed: {error}";
        }
        catch (Exception ex)
        {
            TestStatus = $"Tempo error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ── Test TogglTrack ───────────────────────────────────────────────────────

    private async Task TestTogglTrackAsync(CancellationToken ct)
    {
        IsTesting = true;
        TestStatus = "Testing TogglTrack connection…";

        try
        {
            var token = string.IsNullOrEmpty(TogglTrackApiToken)
                ? _settingsService.GetTogglTrackApiToken()
                : TogglTrackApiToken;

            if (string.IsNullOrEmpty(token))
            {
                TestStatus = "TogglTrack API token is empty.";
                return;
            }

            var baseUrl = string.IsNullOrWhiteSpace(TogglTrackBaseUrl)
                ? "https://api.track.toggl.com"
                : TogglTrackBaseUrl.TrimEnd('/');

            using var client = new HttpClient();
            var credential = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{token.Trim()}:api_token"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credential);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync($"{baseUrl}/api/v9/me", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var me = System.Text.Json.JsonSerializer.Deserialize<WorklogManager.Models.TogglMeResponse>(
                    body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                TestStatus = $"TogglTrack OK — connected as {me?.Fullname ?? me?.Email ?? "unknown"}" +
                             $"  (workspace {me?.DefaultWorkspaceId})";
            }
            else
            {
                var shortBody = body.Length > 400 ? body[..400] + "…" : body;
                TestStatus = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                             $"URL: {baseUrl}/api/v9/me\n\n" +
                             $"Response body:\n{shortBody}";
            }
        }
        catch (Exception ex)
        {
            TestStatus = $"TogglTrack error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        var s = _settingsService.Load();
        JiraBaseUrl = !string.IsNullOrEmpty(s.JiraBaseUrl)
            ? s.JiraBaseUrl
            : "https://tcm-international.atlassian.net";

        // Prefer saved value; fall back to environment variable
        JiraEmail = !string.IsNullOrEmpty(s.JiraEmail)
            ? s.JiraEmail
            : Environment.GetEnvironmentVariable("JIRA_USERNAME") ?? string.Empty;

        // Pre-populate from env var only when no saved token exists yet
        var savedJiraToken = _settingsService.GetJiraApiToken();
        JiraApiToken = string.IsNullOrEmpty(savedJiraToken)
            ? Environment.GetEnvironmentVariable("JIRA_API_TOKEN") ?? string.Empty
            : string.Empty;

        // Env var takes priority; otherwise leave blank (saved token is used implicitly)
        TempoApiToken = Environment.GetEnvironmentVariable("TEMPO_API_TOKEN") ?? string.Empty;

        PapasMode = s.PapasMode;

        TogglTrackEmail = !string.IsNullOrEmpty(s.TogglTrackEmail)
            ? s.TogglTrackEmail
            : Environment.GetEnvironmentVariable("TOGGL_USERNAME") ?? string.Empty;
        TogglTrackBaseUrl = !string.IsNullOrEmpty(s.TogglTrackBaseUrl)
            ? s.TogglTrackBaseUrl
            : "https://api.track.toggl.com";

        var savedTogglToken = _settingsService.GetTogglTrackApiToken();
        TogglTrackApiToken = string.IsNullOrEmpty(savedTogglToken)
            ? Environment.GetEnvironmentVariable("TOGGL_API_TOKEN") ?? string.Empty
            : string.Empty;
    }
}
