using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using WorklogManager.Helpers;
using WorklogManager.Models;
using WorklogManager.Services;

namespace WorklogManager.ViewModels;

/// <summary>
/// ViewModel for the main window.
///
/// Data flow:
///   ITimeEntryProvider → GetTimeRecordsAsync (raw rows) → BuildAndRoundRecords (merge + round) → AllRecords → FilteredRecords
///
/// The MergeRecords toggle re-runs BuildAndRoundRecords without re-parsing the file.
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly ITimeEntryProvider _timeEntryProvider;
    private readonly IJiraValidationService _jiraValidator;
    private readonly ITempoApiService _tempoApi;
    private readonly ISettingsService _settingsService;
    private readonly Func<Views.SettingsWindow> _settingsWindowFactory;

    private AsyncRelayCommand? _activeCommand;

    /// <summary>Raw records exactly as parsed from CSV — one per non-zero-duration row.</summary>
    private IReadOnlyList<WorklogRecord> _rawParsedRecords = Array.Empty<WorklogRecord>();

    public MainViewModel(
        ITimeEntryProvider timeEntryProvider,
        IJiraValidationService jiraValidator,
        ITempoApiService tempoApi,
        ISettingsService settingsService,
        Func<Views.SettingsWindow> settingsWindowFactory)
    {
        _timeEntryProvider = timeEntryProvider;
        _jiraValidator = jiraValidator;
        _tempoApi = tempoApi;
        _settingsService = settingsService;
        _settingsWindowFactory = settingsWindowFactory;

        AllRecords = new ObservableCollection<WorklogRecord>();
        FilteredRecords = new ObservableCollection<WorklogRecord>();
        DateFilters = new ObservableCollection<DateFilterItem>();

        LoadCsvCommand = new AsyncRelayCommand(LoadCsvAsync);
        ValidateIssuesCommand = new AsyncRelayCommand(
            ValidateIssuesAsync,
            () => AllRecords.Count > 0);
        CheckTempoCommand = new AsyncRelayCommand(
            CheckTempoAsync,
            () => AllRecords.Count > 0);
        SendToTempoCommand = new AsyncRelayCommand(
            SendToTempoAsync,
            () => FilteredRecords.Any(r => r.IsSelected && r.IsValid));
        CancelCommand = new RelayCommand(CancelCurrent, () => _activeCommand?.IsExecuting == true);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        SelectAllDatesCommand = new RelayCommand(() => SetAllDates(true));
        ClearAllDatesCommand = new RelayCommand(() => SetAllDates(false));
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<WorklogRecord> AllRecords { get; }
    public ObservableCollection<WorklogRecord> FilteredRecords { get; }
    public ObservableCollection<DateFilterItem> DateFilters { get; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public AsyncRelayCommand LoadCsvCommand { get; }
    public AsyncRelayCommand ValidateIssuesCommand { get; }
    public AsyncRelayCommand CheckTempoCommand { get; }
    public AsyncRelayCommand SendToTempoCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand SelectAllDatesCommand { get; }
    public RelayCommand ClearAllDatesCommand { get; }

    // ── Options ───────────────────────────────────────────────────────────────

    private bool _mergeRecords = true;
    /// <summary>
    /// When true: records are grouped by Date + IssueKey, durations summed,
    /// descriptions formatted as a bulleted list.
    /// When false: one WorklogRecord per CSV row.
    /// Changing this property re-processes the raw CSV data immediately.
    /// </summary>
    public bool MergeRecords
    {
        get => _mergeRecords;
        set
        {
            if (SetProperty(ref _mergeRecords, value) && _rawParsedRecords.Count > 0)
                RefreshRecordsFromRaw();
        }
    }

    private bool _isDryRun;
    public bool IsDryRun
    {
        get => _isDryRun;
        set => SetProperty(ref _isDryRun, value);
    }

    // ── Display properties ────────────────────────────────────────────────────

    private string _exportSummary = "No file loaded";
    public string ExportSummary
    {
        get => _exportSummary;
        private set => SetProperty(ref _exportSummary, value);
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    private string _statusLog = string.Empty;
    public string StatusLog
    {
        get => _statusLog;
        private set => SetProperty(ref _statusLog, value);
    }

    private string _loadedFilePath = string.Empty;
    public string LoadedFilePath
    {
        get => _loadedFilePath;
        private set => SetProperty(ref _loadedFilePath, value);
    }

    /// <summary>Shown in the bottom bar after Validate Issues completes.</summary>
    private string _validationSummary = string.Empty;
    public string ValidationSummary
    {
        get => _validationSummary;
        private set
        {
            SetProperty(ref _validationSummary, value);
            OnPropertyChanged(nameof(ValidationSummaryIsSuccess));
        }
    }

    /// <summary>True when all issues were found — drives green colour in the UI.</summary>
    public bool ValidationSummaryIsSuccess => _validationSummary.StartsWith("✔");

    // ── Load CSV ──────────────────────────────────────────────────────────────

    private async Task LoadCsvAsync(CancellationToken ct)
    {
        _activeCommand = LoadCsvCommand;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select TogglTrack Detailed Report CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() != true) return;

        AppendLog($"Loading {System.IO.Path.GetFileName(dlg.FileName)}…");
        Progress = 0;

        try
        {
            var papasMode = _settingsService.Load().PapasMode;
            var context = new TimeEntryProviderContext { FilePath = dlg.FileName, PapasMode = papasMode };
            _rawParsedRecords = await _timeEntryProvider.GetTimeRecordsAsync(context, ct);
            LoadedFilePath = dlg.FileName;
            ValidationSummary = string.Empty;

            RefreshRecordsFromRaw();

            AppendLog($"Loaded {AllRecords.Count} records " +
                      $"(from {_rawParsedRecords.Count} CSV rows) " +
                      $"across {DateFilters.Count} days.");
            Progress = 100;
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading CSV: {ex.Message}");
        }
        finally
        {
            _activeCommand = null;
        }
    }

    // ── Validate Issues ───────────────────────────────────────────────────────

    private async Task ValidateIssuesAsync(CancellationToken ct)
    {
        _activeCommand = ValidateIssuesCommand;
        Progress = 0;

        var settings = _settingsService.Load();
        if (!settings.IsJiraConfigured)
        {
            AppendLog("Jira is not configured. Open Settings and enter your credentials.");
            return;
        }

        var keys = AllRecords.Select(r => r.IssueKey).Distinct().ToList();
        AppendLog($"Validating {keys.Count} unique issue keys…");

        // Reset all to unknown while validating
        foreach (var r in AllRecords) r.IssueExists = null;
        ValidationSummary = string.Empty;

        try
        {
            var progressReporter = new Progress<int>(p => Progress = p);
            var results = await _jiraValidator.ValidateIssuesAsync(keys, progressReporter, ct);

            // Build issue-ID lookup from cache (populated during ValidateIssuesAsync)
            var issueIdTasks = keys.ToDictionary(
                k => k,
                k => _jiraValidator.GetIssueIdAsync(k, ct));
            await Task.WhenAll(issueIdTasks.Values);

            foreach (var r in AllRecords)
            {
                if (results.TryGetValue(r.IssueKey, out bool exists))
                    r.IssueExists = exists;
                if (issueIdTasks.TryGetValue(r.IssueKey, out var idTask))
                    r.JiraIssueId = idTask.Result;
            }

            int found   = results.Count(kvp => kvp.Value);
            int missing = results.Count(kvp => !kvp.Value);

            // Persistent notification visible in the main window
            ValidationSummary = missing == 0
                ? $"✔  All {found} issues exist in Jira  ({DateTime.Now:HH:mm})"
                : $"⚠  {found} found · {missing} missing in Jira  ({DateTime.Now:HH:mm})";

            AppendLog($"Validation complete: {found} found ✔, {missing} not found ❌");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Validation cancelled.");
            throw;
        }
        finally
        {
            _activeCommand = null;
            UpdateSummary();
        }
    }

    // ── Check Tempo ───────────────────────────────────────────────────────────

    private async Task CheckTempoAsync(CancellationToken ct)
    {
        _activeCommand = CheckTempoCommand;
        Progress = 0;

        var settings = _settingsService.Load();
        if (!settings.IsTempoConfigured)
        {
            AppendLog("Tempo API token is not configured. Open Settings.");
            return;
        }
        if (!settings.IsJiraConfigured)
        {
            AppendLog("Jira is not configured (needed to resolve account ID). Open Settings.");
            return;
        }

        AppendLog("Checking Tempo for existing worklogs…");

        try
        {
            var authorAccountId = await _jiraValidator.GetCurrentUserAccountIdAsync(ct);
            if (string.IsNullOrEmpty(authorAccountId))
            {
                AppendLog("Could not resolve Jira account ID. Check your Jira credentials.");
                return;
            }

            var dates = AllRecords.Select(r => r.Date.Date).ToList();
            var from = dates.Min().ToString("yyyy-MM-dd");
            var to   = dates.Max().ToString("yyyy-MM-dd");

            AppendLog($"Fetching Tempo worklogs for {from} → {to}…");

            var (success, existing, error) =
                await _tempoApi.GetExistingWorklogsAsync(authorAccountId, from, to, ct);

            if (!success)
            {
                AppendLog($"Failed to fetch Tempo worklogs: {error}");
                return;
            }

            int alreadyExists = 0;
            foreach (var record in AllRecords)
            {
                bool found = existing!.Contains((record.DateDisplay, record.IssueKey));
                record.ExistsInTempo = found;
                if (found)
                {
                    record.IsSelected    = false;
                    record.UploadStatus  = "Already exists";
                    alreadyExists++;
                }
                else if (record.UploadStatus == "Already exists")
                {
                    // Reset if a previous check had marked it but it no longer matches
                    record.UploadStatus = "Pending";
                }
            }

            int newCount = AllRecords.Count - alreadyExists;
            AppendLog($"Tempo check complete: {alreadyExists} already exist (unchecked), " +
                      $"{newCount} new.");
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            AppendLog("Tempo check cancelled.");
            throw;
        }
        finally
        {
            _activeCommand = null;
            UpdateSummary();
        }
    }

    // ── Send to Tempo ─────────────────────────────────────────────────────────

    private async Task SendToTempoAsync(CancellationToken ct)
    {
        _activeCommand = SendToTempoCommand;

        var settings = _settingsService.Load();
        if (!IsDryRun && !settings.IsTempoConfigured)
        {
            AppendLog("Tempo API token is not configured. Open Settings.");
            return;
        }

        var toUpload = FilteredRecords
            .Where(r => r.IsSelected && r.IsValid && r.IssueExists != false)
            .ToList();

        if (toUpload.Count == 0)
        {
            AppendLog("No valid records selected for upload.");
            return;
        }

        AppendLog(IsDryRun
            ? $"DRY RUN — would upload {toUpload.Count} records:"
            : $"Uploading {toUpload.Count} records to Tempo…");

        // Resolve author account ID (required by Tempo API v4)
        string authorAccountId = string.Empty;
        if (!IsDryRun)
        {
            var jiraSettings = _settingsService.Load();
            if (jiraSettings.IsJiraConfigured)
            {
                authorAccountId = await _jiraValidator.GetCurrentUserAccountIdAsync(ct) ?? string.Empty;
                if (string.IsNullOrEmpty(authorAccountId))
                    AppendLog("Warning: could not resolve Jira account ID — upload may fail.");
            }

            // Resolve missing issue IDs (required by Tempo API v4)
            var missingIds = toUpload.Where(r => r.JiraIssueId == null).ToList();
            if (missingIds.Count > 0 && jiraSettings.IsJiraConfigured)
            {
                AppendLog($"Resolving issue IDs for {missingIds.Count} records…");
                foreach (var r in missingIds)
                    r.JiraIssueId = await _jiraValidator.GetIssueIdAsync(r.IssueKey, ct);
            }
        }

        Progress = 0;
        int completed = 0;

        foreach (var record in toUpload)
        {
            ct.ThrowIfCancellationRequested();

            record.UploadStatus = "Uploading…";
            var request = TempoWorklogRequest.FromRecord(record, authorAccountId);

            if (IsDryRun)
            {
                await Task.Delay(50, ct);
                record.UploadStatus = "Dry Run";
                AppendLog($"  [DRY RUN] {record.IssueKey} {record.Date:yyyy-MM-dd} " +
                          $"{record.RoundedTimeSpentSeconds / 60} min");
            }
            else
            {
                var (success, error) = await _tempoApi.UploadWorklogAsync(request, ct);

                if (success)
                {
                    record.UploadStatus = "Success";
                    record.UploadError = null;
                    AppendLog($"  ✔ {record.IssueKey} {record.Date:yyyy-MM-dd} " +
                              $"{record.RoundedTimeSpentSeconds / 60} min");
                }
                else
                {
                    record.UploadStatus = "Error";
                    record.UploadError = error;
                    AppendLog($"  ❌ {record.IssueKey}: {error}");
                }
            }

            completed++;
            Progress = completed * 100 / toUpload.Count;
        }

        AppendLog(IsDryRun
            ? "Dry run complete."
            : $"Upload complete: {toUpload.Count(r => r.UploadStatus == "Success")} success, " +
              $"{toUpload.Count(r => r.UploadStatus == "Error")} errors.");

        _activeCommand = null;
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void OpenSettings()
    {
        var win = _settingsWindowFactory();
        win.Owner = Application.Current.MainWindow;
        win.ShowDialog();
    }

    // ── Merge + Round processing ──────────────────────────────────────────────

    /// <summary>
    /// Rebuilds AllRecords and DateFilters from the raw parsed data,
    /// applying the current MergeRecords option and 5-minute rounding.
    /// </summary>
    private void RefreshRecordsFromRaw()
    {
        var records = BuildAndRoundRecords(_rawParsedRecords);

        AllRecords.Clear();
        DateFilters.Clear();

        foreach (var r in records)
            AllRecords.Add(r);

        var uniqueDates = records
            .Select(r => r.Date.Date)
            .Distinct()
            .OrderBy(d => d);

        foreach (var d in uniqueDates)
            DateFilters.Add(new DateFilterItem(d, RebuildFilteredRecords));

        _jiraValidator.ClearCache();
        ValidationSummary = string.Empty;
        RebuildFilteredRecords();
    }

    /// <summary>
    /// Optionally merges raw records by Date + IssueKey, then applies 5-minute rounding per day.
    /// </summary>
    private List<WorklogRecord> BuildAndRoundRecords(IReadOnlyList<WorklogRecord> rawRecords)
    {
        List<WorklogRecord> records;

        if (MergeRecords)
        {
            // Group by Date + IssueKey, sum durations, build formatted description
            records = rawRecords
                .GroupBy(r => (r.Date.Date, r.IssueKey))
                .Select(g =>
                {
                    var totalSecs = g.Sum(r => r.OriginalTimeSpentSeconds);
                    return new WorklogRecord
                    {
                        Date        = g.Key.Date,
                        IssueKey    = g.Key.IssueKey,
                        ProjectName = g.First().ProjectName,
                        Description = BuildMergedDescription(g.Select(r => r.Description)),
                        StartTime   = g.Where(r => !string.IsNullOrEmpty(r.StartTime))
                                       .Select(r => r.StartTime)
                                       .OrderBy(t => t)
                                       .FirstOrDefault() ?? string.Empty,
                        OriginalTimeSpentSeconds = totalSecs,
                        RoundedTimeSpentSeconds  = totalSecs,
                        IsSelected   = true,
                        UploadStatus = "Pending"
                    };
                })
                .OrderBy(r => r.Date)
                .ThenBy(r => r.IssueKey)
                .ToList();
        }
        else
        {
            // One record per CSV row — clone so editing doesn't affect _rawParsedRecords
            records = rawRecords
                .Select(r => new WorklogRecord
                {
                    Date        = r.Date,
                    IssueKey    = r.IssueKey,
                    ProjectName = r.ProjectName,
                    Description = r.Description,
                    StartTime   = r.StartTime,
                    OriginalTimeSpentSeconds = r.OriginalTimeSpentSeconds,
                    RoundedTimeSpentSeconds  = r.OriginalTimeSpentSeconds,
                    IsSelected   = true,
                    UploadStatus = "Pending"
                })
                .ToList();
        }

        // Apply 5-minute rounding per day
        foreach (var dayGroup in records.GroupBy(r => r.Date.Date))
            TimeRoundingHelper.ApplyDayRounding(dayGroup.ToList());

        return records;
    }

    /// <summary>
    /// Combines activity descriptions into a bulleted multi-line list.
    /// Each description is trimmed, capitalised, and prefixed with "- ".
    /// Duplicate descriptions (case-insensitive) are removed.
    /// </summary>
    private static string BuildMergedDescription(IEnumerable<string> descriptions)
    {
        var lines = descriptions
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Select(d => d.Length > 0 ? char.ToUpperInvariant(d[0]) + d[1..] : d)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(d => $"- {d}");

        return string.Join('\n', lines);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    private void RebuildFilteredRecords()
    {
        var checkedDates = DateFilters
            .Where(d => d.IsChecked)
            .Select(d => d.Date.Date)
            .ToHashSet();

        FilteredRecords.Clear();
        foreach (var r in AllRecords.Where(r => checkedDates.Contains(r.Date.Date)))
            FilteredRecords.Add(r);

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (AllRecords.Count == 0)
        {
            ExportSummary = "No file loaded";
            return;
        }

        int selectedDays    = DateFilters.Count(d => d.IsChecked);
        int totalDays       = DateFilters.Count;
        int selectedRecords = FilteredRecords.Count(r => r.IsSelected);
        int totalRecords    = AllRecords.Count;
        double totalHours   = FilteredRecords.Where(r => r.IsSelected).Sum(r => r.RoundedHours);

        ExportSummary = $"Export: {selectedDays} / {totalDays} days   " +
                        $"Records: {selectedRecords} / {totalRecords}   " +
                        $"Total: {totalHours:F2} h";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CancelCurrent() => _activeCommand?.Cancel();

    private void SetAllDates(bool selected)
    {
        foreach (var d in DateFilters) d.IsChecked = selected;
    }

    private readonly StringBuilder _logBuilder = new();
    private void AppendLog(string message)
    {
        _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        StatusLog = _logBuilder.ToString();
    }
}
