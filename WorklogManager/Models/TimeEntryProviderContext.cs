namespace WorklogManager.Models;

public record TimeEntryProviderContext
{
    public string? FilePath { get; init; }

    /// <summary>
    /// When true, the Jira issue key is extracted from the Description column
    /// instead of the Project column.
    /// </summary>
    public bool PapasMode { get; init; }
}
