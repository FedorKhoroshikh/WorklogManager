using WorklogManager.Models;

namespace WorklogManager.Services;

public interface ICsvParserService
{
    /// <summary>
    /// Parses a TogglTrack Detailed Report CSV file.
    /// Returns one WorklogRecord per non-zero-duration row (no aggregation or rounding).
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <param name="papasMode">
    /// When true, extracts the Jira issue key from the Description column
    /// (e.g. "WT-13065 some text") instead of the Project column.
    /// </param>
    Task<IReadOnlyList<WorklogRecord>> ParseAsync(string filePath, bool papasMode = false);
}
