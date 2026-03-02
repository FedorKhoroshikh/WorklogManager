using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WorklogManager.Models;

/// <summary>
/// Aggregated worklog entry (one per Date + IssueKey combination from TogglTrack CSV).
/// Displayed as an editable row in the DataGrid and sent to Tempo API.
///
/// Implements IDataErrorInfo so WPF DataGrid can show inline validation errors.
/// </summary>
public class WorklogRecord : INotifyPropertyChanged, IDataErrorInfo
{
    private static readonly Regex IssueKeyRegex =
        new(@"^[A-Z]+-\d+$", RegexOptions.Compiled);

    // ── Core data ────────────────────────────────────────────────────────────

    /// <summary>Project name from the "Project" CSV column (part after IssueKey). Read-only display field.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Start time from the "Start time" CSV column, stored as "HH:mm:ss".
    /// Empty when not available (e.g. merged records with no common time).
    /// Passed to Tempo API as the optional startTime field.
    /// </summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>Start time formatted for display as "HH:mm". Empty when StartTime is not set.</summary>
    public string StartTimeDisplay =>
        StartTime.Length >= 5 ? StartTime[..5] : StartTime;

    private DateTime _date;
    public DateTime Date
    {
        get => _date;
        set { _date = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateDisplay)); }
    }

    public string DateDisplay => Date.ToString("yyyy-MM-dd");

    private string _issueKey = string.Empty;
    public string IssueKey
    {
        get => _issueKey;
        set
        {
            _issueKey = (value ?? string.Empty).Trim().ToUpperInvariant();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    /// <summary>Original aggregated seconds from CSV — never changed after parse.</summary>
    public int OriginalTimeSpentSeconds { get; set; }

    /// <summary>
    /// Seconds after 5-minute rounding. Initially set by TimeRoundingHelper,
    /// then editable by the user (snaps to nearest 5 min on assignment via RoundedHours).
    /// </summary>
    private int _roundedTimeSpentSeconds;
    public int RoundedTimeSpentSeconds
    {
        get => _roundedTimeSpentSeconds;
        set
        {
            _roundedTimeSpentSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RoundedHours));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    /// <summary>
    /// Rounded hours as decimal (e.g. 1.5 = 90 min).
    /// Editing this value automatically snaps to the nearest 5-minute boundary.
    /// This is the column the user edits in the DataGrid.
    /// </summary>
    public double RoundedHours
    {
        get => Math.Round(RoundedTimeSpentSeconds / 3600.0, 4);
        set
        {
            // Convert decimal hours → seconds → snap to nearest 5 min
            int totalSeconds = (int)Math.Round(value * 3600);
            int totalMinutes = totalSeconds / 60;
            int remainder = totalMinutes % 5;
            int snappedMinutes = remainder < 3
                ? totalMinutes - remainder        // round down
                : totalMinutes + (5 - remainder); // round up
            RoundedTimeSpentSeconds = Math.Max(300, snappedMinutes * 60); // minimum 5 min
        }
    }

    // ── UI state ─────────────────────────────────────────────────────────────

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>Numeric Jira issue ID required by Tempo API v4. Populated during Jira validation.</summary>
    public int? JiraIssueId { get; set; }

    private bool? _issueExists;
    public bool? IssueExists
    {
        get => _issueExists;
        set
        {
            _issueExists = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IssueExistsDisplay));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public string IssueExistsDisplay => IssueExists switch
    {
        true  => "✔",
        false => "❌",
        null  => "—"
    };

    /// <summary>
    /// Set to true by CheckTempoCommand when a worklog for this (Date, IssueKey)
    /// already exists in Tempo. Drives the yellow row highlight and auto-unchecks the row.
    /// </summary>
    private bool _existsInTempo;
    public bool ExistsInTempo
    {
        get => _existsInTempo;
        set { _existsInTempo = value; OnPropertyChanged(); }
    }

    private string _uploadStatus = "Pending";
    public string UploadStatus
    {
        get => _uploadStatus;
        set { _uploadStatus = value; OnPropertyChanged(); }
    }

    private string? _uploadError;
    public string? UploadError
    {
        get => _uploadError;
        set { _uploadError = value; OnPropertyChanged(); OnPropertyChanged(nameof(UploadStatusDisplay)); }
    }

    public string UploadStatusDisplay =>
        string.IsNullOrEmpty(UploadError) ? UploadStatus : $"{UploadStatus}: {UploadError}";

    // ── Validation ───────────────────────────────────────────────────────────

    public bool IsValid =>
        IssueKeyRegex.IsMatch(IssueKey) &&
        !string.IsNullOrWhiteSpace(Description) &&
        RoundedTimeSpentSeconds > 0 &&
        Date != default;

    // IDataErrorInfo — used by WPF DataGrid to show inline validation errors
    public string this[string columnName] => columnName switch
    {
        nameof(IssueKey) =>
            IssueKeyRegex.IsMatch(IssueKey) ? string.Empty : "Format must be ABC-123",
        nameof(Description) =>
            string.IsNullOrWhiteSpace(Description) ? "Description is required" : string.Empty,
        nameof(RoundedHours) =>
            RoundedTimeSpentSeconds > 0 ? string.Empty : "Hours must be > 0",
        _ => string.Empty
    };

    public string Error => string.Empty;

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
