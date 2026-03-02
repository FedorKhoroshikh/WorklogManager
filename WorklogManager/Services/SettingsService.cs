using System.IO;
using System.Text.Json;
using WorklogManager.Helpers;
using WorklogManager.Models;

namespace WorklogManager.Services;

/// <summary>
/// Persists application settings to %AppData%\WorklogManager\settings.json.
/// API tokens are encrypted with DPAPI before storage and decrypted on retrieval.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WorklogManager", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true };

    private AppSettings? _cached;

    public AppSettings Load()
    {
        if (_cached != null) return _cached;

        if (!File.Exists(SettingsPath))
        {
            _cached = new AppSettings();
            return _cached;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            _cached = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _cached = new AppSettings();
        }

        return _cached;
    }

    public void Save(AppSettings settings)
    {
        _cached = settings;

        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public string GetJiraApiToken()
        => CredentialHelper.Decrypt(Load().JiraApiTokenEncrypted);

    public string GetTempoApiToken()
        => CredentialHelper.Decrypt(Load().TempoApiTokenEncrypted);

    public string GetTogglTrackApiToken()
        => CredentialHelper.Decrypt(Load().TogglTrackApiTokenEncrypted);
}
