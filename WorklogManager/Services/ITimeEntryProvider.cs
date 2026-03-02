using WorklogManager.Models;

namespace WorklogManager.Services;

public interface ITimeEntryProvider
{
    Task<IReadOnlyList<WorklogRecord>> GetTimeRecordsAsync(
        TimeEntryProviderContext context,
        CancellationToken cancellationToken = default);
}
