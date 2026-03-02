namespace WorklogManager.Models;

public record TimeEntryProviderContext
{
    public string? FilePath { get; init; }

    /// <summary>
    /// When true, the Jira issue key is extracted from the Description column
    /// instead of the Project column.
    /// </summary>
    public bool PapasMode { get; init; }

    /// <summary>Inclusive start date for TogglTrack API import.</summary>
    public DateOnly? DateFrom { get; init; }

    /// <summary>Inclusive end date for TogglTrack API import.</summary>
    public DateOnly? DateTo { get; init; }
}
