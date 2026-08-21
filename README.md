# Jetset

Jetset is a personal productivity workspace for Windows: a visible clock, focused work sessions, task and project planning, context preservation, and productivity analytics — all local, with no accounts or cloud sync.

It helps you keep time in view, measure how long tasks actually take, pause without counting idle time, preserve working context across switches, and review trends in your focused work.

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet restore
dotnet build
```

## Run

```bash
dotnet run --project src/Jetset.App/Jetset.App.csproj
```

## Test

```bash
dotnet test
```

## Publish (self-contained Windows x64)

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use the publish profile:

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -p:PublishProfile=win-x64
```

The output is under `src/Jetset.App/bin/Release/net10.0-windows/win-x64/publish/`.

## Database location

SQLite database:

`%LocalAppData%\Jetset\jetset.db`

Before schema upgrades, Jetset creates a timestamped backup alongside the database file (for example `jetset.db.backup-20260822120000`).

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` | Start new work session |
| `Ctrl+P` | Pause or resume active session |
| `Ctrl+Enter` | Finish active session |
| `Ctrl+M` | Toggle compact mode |
| `Ctrl+H` | Show or hide the main window |

Shortcuts are also listed in **Settings**.

## Features

### Focus and sessions

- Idle / running / paused main-window states
- Stopwatch (active time only) and countdown (absolute end time, overtime after zero)
- One active session at a time; additional paused sessions appear in the waiting queue
- Task-linked sessions with quick-task creation at start
- Optional context capture when pausing, switching, or finishing
- Today's history and total productive duration
- Optional session notes and history edits
- Always-on-top, compact mode, remember window position/size
- Minimize to system tray (close hides; Exit from tray quits)
- Optional start with Windows
- 12/24-hour clock, optional seconds, light/dark theme
- Interrupted-session recovery after restart
- Auto-pause on idle with optional auto-resume

### Tasks and planning

- Quick tasks without a project
- Projects, milestones, and subtasks
- Task lifecycle: active, blocked, done, cancelled
- Working context fields: status, progress, next action, blocker
- Global search across tasks and context

### Analytics

- Focus time by task and date range
- Activity heatmap
- Task switch metrics
- Project momentum trends

### Upgrading from V1

Existing Jetset databases upgrade automatically on first launch. Historical sessions are linked to tasks by task name, and a welcome dialog introduces V2 navigation and shortcuts.

## Solution structure

```text
Jetset.sln
src/Jetset.App/     WPF application
tests/Jetset.Tests/ xUnit timing, session, migration, and analytics tests
```

## Documentation

- [DOMAIN.md](./DOMAIN.md) — V2 product domain and design principles
- [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) — vertical slice delivery plan
