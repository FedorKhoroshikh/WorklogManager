# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**WorklogManager** is a WPF desktop application (.NET 8, `net8.0-windows`) that bulk-imports time tracking
data from TogglTrack — either via CSV exports or directly via the TogglTrack Reports API — into Jira/Tempo
Cloud as worklog entries.

## Build & Run Commands

```bash
dotnet build
dotnet run --project WorklogManager/WorklogManager.csproj
dotnet publish -c Release -o ./publish
```

There are no automated tests or linting tools configured in this project.

## Architecture

**Pattern:** MVVM with interface-based services and `Microsoft.Extensions.DependencyInjection`.

**Data flow:**

```
Time entry source                                  WorklogRecord list
─────────────────                                  ──────────────────
TogglTrack CSV  → CsvParserService            ┐
                  → TogglTrackCsvTimeEntryProvider ┤
TogglTrack API  → TogglTrackApiTimeEntryProvider ┘ → MainViewModel
                                                       → (optional merge by Date+IssueKey)
                                                       → TimeRoundingHelper (5-min rounding)
                                                       → AllRecords (DataGrid display)
                                                       → JiraValidationService (validate keys, fetch IDs)
                                                       → TempoApiService (upload worklogs)
```

Time entry providers implement `ITimeEntryProvider.GetTimeRecordsAsync(TimeEntryProviderContext, …)`,
so the CSV path and the Reports-API path are interchangeable from `MainViewModel`'s perspective.

**DI registration (`App.xaml.cs`):**
- Singleton: `ISettingsService`, `ICsvParserService`, `ITimeEntryProvider` (CSV provider)
- Typed `HttpClient`: `IJiraValidationService`, `ITempoApiService`, `TogglTrackApiTimeEntryProvider`
- Transient: `MainViewModel`, `SettingsViewModel`, `MainWindow`, `SettingsWindow`
- `Func<SettingsWindow>` factory so `MainViewModel` can open the settings dialog without touching `IServiceProvider`

**Settings persistence:** `%AppData%\WorklogManager\settings.json`. API tokens (Jira, Tempo, TogglTrack)
are encrypted with Windows DPAPI via `Helpers/CredentialHelper.cs`.

## Key Business Logic

**CSV parsing (`CsvParserService`):** Uses CsvHelper. Extracts the issue key from the Project column with
regex `^([A-Z]+-\d+)\s+(.*)`. Zero-duration entries are filtered out.

**TogglTrack Reports API (`TogglTrackApiTimeEntryProvider`):** Authenticated with the user's API token,
date-range filtered, paginated through `Reports v3 detailed` endpoint. Same `WorklogRecord` shape as CSV.

**Time rounding (`Helpers/TimeRoundingHelper.cs`):** Two related operations.
- `ApplyDayRounding` — rounds each entry to the nearest 5 min and compensates so the daily sum does not
  drop below the original (adds 5 min to the biggest downward-rounded record).
- `RoundTimestampsAndDurations` — also rounds **start times** to the nearest 5 min, redistributes the
  resulting delta to adjacent durations to preserve timeline continuity, rounds all durations, then
  iteratively resolves overlaps (shrink the longer entry, or shrink + shift the later entry forward by 5 min).
  Minimum duration is 5 min.

**Jira integration (REST API v3, Basic auth):** Validates issue existence, retrieves the numeric issue ID
(required by Tempo v4) and the author's `accountId`. Concurrency is limited by `SemaphoreSlim(5)`; results
are cached to avoid redundant calls.

**Tempo integration (Cloud API v4, Bearer token):** `POST /4/worklogs` for upload; auto-paginated
`GET /4/worklogs/user/{accountId}` for existing-worklog lookup. Retries up to 3× with exponential backoff;
respects `Retry-After` on HTTP 429.

**WorklogRecord validation:** Implements `IDataErrorInfo` for inline DataGrid validation — issue key format
`^[A-Z]+-\d+$`, non-empty description, hours > 0. The `End time` column is computed from
`StartTime + RoundedTimeSpentSeconds`.

**Date-range dialog (`Views/DateRangeWindow.xaml`):** Calendar pickers used by the Toggl API import path;
`From` and `To` both default to today.
