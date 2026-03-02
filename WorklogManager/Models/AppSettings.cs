namespace WorklogManager.Models;

/// <summary>
/// Application settings persisted to %AppData%\WorklogManager\settings.json.
/// API tokens are stored encrypted (DPAPI).
/// </summary>
public class AppSettings
{
    public string JiraEmail { get; set; } = string.Empty;

    /// <summary>Jira API token encrypted with DPAPI. Use ISettingsService.GetJiraApiToken() to decrypt.</summary>
    public string JiraApiTokenEncrypted { get; set; } = string.Empty;

    /// <summary>Tempo API token encrypted with DPAPI. Use ISettingsService.GetTempoApiToken() to decrypt.</summary>
    public string TempoApiTokenEncrypted { get; set; } = string.Empty;

    public string JiraBaseUrl { get; set; } = string.Empty;

    public bool IsJiraConfigured =>
        !string.IsNullOrWhiteSpace(JiraEmail) &&
        !string.IsNullOrWhiteSpace(JiraApiTokenEncrypted) &&
        !string.IsNullOrWhiteSpace(JiraBaseUrl);

    public bool IsTempoConfigured =>
        !string.IsNullOrWhiteSpace(TempoApiTokenEncrypted);

    /// <summary>
    /// When true, the Jira issue key is extracted from the Description column
    /// (format: "WT-13065 some text") instead of the Project column.
    /// </summary>
    public bool PapasMode { get; set; }
}
