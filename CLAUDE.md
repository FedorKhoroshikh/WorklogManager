# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**WorklogManager** is a WPF desktop application (.NET 8) that bulk-imports time tracking data from TogglTrack CSV exports into Jira/Tempo Cloud.

## Build & Run Commands

```bash
dotnet build
dotnet run --project WorklogManager/WorklogManager.csproj
dotnet publish -c Release -o ./publish
```

There are no automated tests or linting tools configured in this project.

## Architecture

**Pattern:** MVVM with interface-based services and Microsoft.Extensions.DependencyInjection.

**Data flow:**

```
TogglTrack CSV → CsvParserService → WorklogRecord list
    → MainViewModel (optional merge by Date+IssueKey)
    → TimeRoundingHelper (5-min rounding, daily total preservation)
    → AllRecords (DataGrid display)
    → JiraValidationService (validates issue keys, fetches numeric IDs)
    → TempoApiService (check existing / upload worklogs)
```

**DI registration (App.xaml.cs):**
- Singleton: `ISettingsService`, `ICsvParserService`
- Named HttpClients: `IJiraValidationService`, `ITempoApiService`
- Transient: ViewModels, Windows; `Func<SettingsWindow>` factory for dialogs

**Settings persistence:** `%AppData%\WorklogManager\settings.json`; API tokens encrypted with Windows DPAPI via `CredentialHelper`.

## Key Business Logic

**CSV parsing:** Extracts issue key from the Project column using regex `^([A-Z]+-\d+)\s+(.*)`. Zero-duration entries are filtered out.

**Time rounding (`TimeRoundingHelper`):** Rounds each entry to nearest 5 minutes, then applies a compensation pass so the daily sum never falls below the original total (adds 5 min to the record with the largest downward rounding).

**Jira integration (REST API v3, Basic auth):** Validates issue existence, retrieves numeric issue ID (required by Tempo v4) and the author's `accountId`.

**Tempo integration (Cloud API v4, Bearer token):** `POST /4/worklogs` for upload; `GET /4/worklogs/user/{accountId}` with auto-pagination for existing worklogs. Retries up to 3× with exponential backoff; respects `Retry-After` on 429.

**WorklogRecord validation:** Implements `IDataErrorInfo` for inline DataGrid validation — issue key format `^[A-Z]+-\d+$`, non-empty description, hours > 0.

**Jira validation concurrency:** `SemaphoreSlim(5)` limits parallel HTTP requests; results are cached to avoid redundant calls.
