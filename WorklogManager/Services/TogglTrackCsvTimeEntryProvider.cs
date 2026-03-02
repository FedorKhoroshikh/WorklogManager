using WorklogManager.Models;

namespace WorklogManager.Services;

public class TogglTrackCsvTimeEntryProvider : ITimeEntryProvider
{
    private readonly ICsvParserService _csvParser;

    public TogglTrackCsvTimeEntryProvider(ICsvParserService csvParser)
    {
        _csvParser = csvParser;
    }

    public Task<IReadOnlyList<WorklogRecord>> GetTimeRecordsAsync(
        TimeEntryProviderContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.FilePath))
            throw new ArgumentException("FilePath must be set for CSV import.", nameof(context));

        return _csvParser.ParseAsync(context.FilePath, context.PapasMode);
    }
}
