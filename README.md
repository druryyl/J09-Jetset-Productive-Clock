# Jetset

A **Personal Execution Workspace** for Windows — capture tasks quickly, execute one at a time, preserve project context across switches, and track focused work time. Everything runs locally on your machine: no accounts, no cloud sync, no subscription.

Jetset is a personal productivity tool, not project management software. It helps you capture work in seconds, pick what to run next, keep project context in one place, and review how you actually spend focused time.

## Table of contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Usage](#usage)
- [Data and privacy](#data-and-privacy)
- [Upgrading from V1](#upgrading-from-v1)
- [Development](#development)
- [Project structure](#project-structure)
- [Documentation](#documentation)
- [License](#license)

## Features

Jetset is organized into four main areas: **Focus**, **Tasks**, **Projects**, and **Analytics**.

### Focus — execute and track time

- Always-visible clock with 12/24-hour format and optional seconds
- **One Running task at a time** — starting another task stops the previous one
- **Quick Capture** to Inbox without disturbing your Running task (`Ctrl+Shift+C`)
- Ready and Waiting task lists for fast switching from the Focus view
- **Switch and mark waiting** when a blocked task needs to stay blocked
- Project context displayed while a Running task belongs to a project
- **Stopwatch** mode (active time only) and **countdown** mode (absolute end time, with overtime after zero)
- Session states: idle, running, and paused
- Today's session history and total productive duration on the Focus view
- Optional session notes and history edits
- Interrupted-session recovery after restart
- Auto-pause on system idle, with optional auto-resume
- Light and dark themes
- Always-on-top, compact mode, and remembered window position and size
- Minimize to system tray (closing the window hides it; use **Exit** from the tray to quit)
- Optional start with Windows
- Optional sound when a countdown completes

### Tasks — capture and organize

- **Quick Capture** to Inbox — title only, no project required
- Six-state lifecycle: Inbox, Ready, Running, Waiting, Done, Cancelled
- Planned vs unplanned origin badges
- Optional project assignment; tasks can also stand alone
- Status filters for Inbox, Ready, Running, Waiting, Done, and Cancelled
- Start work or switch tasks directly from the task list
- Optional task notes
- Global search across task titles and project context

### Projects — group work and hold context

- Optional project grouping for related tasks
- **Project context** — a single free-form `ContextText` field edited independently of tasks and sessions
- Task list per project with start/resume actions
- Optional deadline metadata (not a planning driver)

### Analytics — personal awareness

- Daily focus time summary for a selected date
- Per-task focus time breakdown
- Activity heatmap (12-week view)
- Current and longest productive streaks

## Prerequisites

- **Windows 10 or 11**
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)**

## Quick start

Clone the repository, then build and run:

```bash
dotnet restore
dotnet build
dotnet run --project src/Jetset.App/Jetset.App.csproj
```

Run the test suite:

```bash
dotnet test
```

## Usage

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+Shift+C` | Focus Quick Capture (capture to Inbox without switching tasks) |
| `Ctrl+N` | Start work on the selected task |
| `Ctrl+P` | Pause or resume the active session |
| `Ctrl+Enter` | Finish the active session |
| `Ctrl+M` | Toggle compact mode |
| `Ctrl+H` | Show or hide the main window |

Shortcuts are also listed in **Settings**.

### Typical workflow

1. **Capture** — press `Ctrl+Shift+C` or use Quick Capture on Focus/Tasks to add items to Inbox without interrupting your Running task.
2. **Organize** — move tasks from Inbox to Ready, assign optional projects, and edit project context on the Projects view.
3. **Execute** — open **Focus**, start one task, and work with the stopwatch or countdown timer. Pause (`Ctrl+P`) when you step away.
4. **Switch** — pick the next task from Ready, or use **Switch and mark waiting** when the previous task is blocked.
5. **Review** — check focus time, heatmap, and streaks in **Analytics**.

## Data and privacy

All data is stored locally in a SQLite database:

```text
%LocalAppData%\Jetset\jetset.db
```

Before schema upgrades, Jetset creates a timestamped backup alongside the database file (for example `jetset.db.backup-20260822120000`). No data is sent to external services by the application.

## Upgrading from V1

Existing Jetset databases upgrade automatically on first launch after installing V2. Historical sessions are linked to tasks by task name. Task statuses are remapped to the new lifecycle (for example, blocked tasks become Waiting). A welcome dialog introduces V2 navigation and keyboard shortcuts on first run.

## Development

### Technology

| Layer | Stack |
| --- | --- |
| Runtime | .NET 10 (`net10.0-windows`), Windows desktop |
| UI | WPF with MVVM |
| Persistence | SQLite via `Microsoft.Data.Sqlite` |
| Tests | xUnit |

### Publish (self-contained Windows x64)

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use the publish profile:

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -p:PublishProfile=win-x64
```

Published output:

```text
src/Jetset.App/bin/Release/net10.0-windows/win-x64/publish/
```

### Test coverage

Tests live in `tests/Jetset.Tests/` and cover session timing, idle auto-pause, task lifecycle, project context, work execution, analytics, migrations, and related services.

## Project structure

```text
Jetset.sln
src/Jetset.App/       WPF application (Views, ViewModels, Services, Persistence)
tests/Jetset.Tests/   xUnit test project
DOMAIN.md             Product domain specification (source of truth)
IMPLEMENTATION_PLAN.md  Domain realignment plan v2.1 (supersedes prior roadmap)
```

## Documentation

- [DOMAIN.md](./DOMAIN.md) — **Source of truth** — product domain and design principles
- [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) — Domain realignment plan v2.1 (supersedes Implementation Plan v2.0 and invalid slices S-03–S-13, S-17, S-18)

## License

This project is licensed under the [MIT License](./LICENSE.txt).
