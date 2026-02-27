using WorklogManager.Models;

namespace WorklogManager.Services;

public interface ICsvParserService
{
    /// <summary>
    /// Parses a TogglTrack Detailed Report CSV file.
    /// Aggregates rows by (Start date, IssueKey) and applies 5-minute rounding per day.
    /// </summary>
    Task<IReadOnlyList<WorklogRecord>> ParseAsync(string filePath);
}
