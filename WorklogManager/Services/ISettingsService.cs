using WorklogManager.Models;

namespace WorklogManager.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);

    /// <summary>Returns the decrypted Jira API token.</summary>
    string GetJiraApiToken();

    /// <summary>Returns the decrypted Tempo API token.</summary>
    string GetTempoApiToken();
}
