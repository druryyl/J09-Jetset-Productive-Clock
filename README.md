# Jetset

A **Personal Execution Workspace** for Windows — capture tasks quickly, organize work in a hierarchical tree, execute one task at a time via a Running Task Bar, and preserve project context in an adjacent panel. Everything runs locally on your machine: no accounts, no cloud sync, no subscription.

Jetset is a personal productivity tool, not project management software. It helps you capture work in seconds, organize it in a Work Tree, convert tasks to projects when work grows, keep project context and deadlines visible, and review how you actually spend focused time.

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

Jetset V2 centers on the **Work Tree Workspace** — a split layout with tree navigation, Context Panel, and Running Task Bar. **Settings** and **Analytics** are secondary.

### Work Tree — organize and navigate

- **Primary workspace** on startup — hierarchical tree of projects and tasks
- **Quick Capture** at tree root to Inbox without disturbing your Running task (`Ctrl+Shift+C`)
- Expand/collapse projects; state persists across sessions
- **Drag-and-drop** — move tasks onto projects or back to root
- **Task ↔ Project conversion** — grow a task into a project container, or collapse an empty project back to a task
- Spent time and optional estimates shown on tree nodes (`18h / 12h` for tasks; rollup for projects)

### Context Panel — project detail at a glance

- Adjacent panel when a project is selected (or when a task-with-project is selected)
- **Project context** — editable `ContextText`, independent of task operations
- **Deadline** visible in normal workflow (project-only)
- **Effort rollup** — derived spent and estimate sums from child tasks
- Hidden or minimal for standalone tasks

### Running Task Bar — execute one task

- **One Running task at a time** — starting another task stops the previous one
- Timer with stopwatch and countdown modes; pause-aware active duration
- **Done**, **Waiting**, and **Pause** controls on the bottom bar
- **Switch and mark waiting** when a blocked task must stay blocked
- Compact overlay mode for minimal timer chrome

### Tasks — lifecycle (via tree or secondary views)

- Six-state lifecycle: Inbox, Ready, Running, Waiting, Done, Cancelled
- Planned vs unplanned origin
- Optional project assignment; tasks can stand alone at tree root
- Optional **task estimate** (minutes); no task-level deadline or context
- Global search across task titles and project context

### Analytics — personal awareness (secondary)

- Daily focus time summary for a selected date
- Per-task focus time breakdown
- Activity heatmap (12-week view)
- Current and longest productive streaks
- Reachable from Settings

### Application chrome

- Light and dark themes
- Always-on-top, compact mode, remembered window position and size
- Minimize to system tray (closing the window hides it; use **Exit** from the tray to quit)
- Optional start with Windows
- Interrupted-session recovery after restart
- Auto-pause on system idle, with optional auto-resume
- Optional sound when a countdown completes

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
| `Ctrl+Shift+C` | Quick Capture to Inbox at tree root (does not switch tasks) |
| `Ctrl+N` | Start work (opens compact overlay task picker) |
| `Ctrl+P` | Pause or resume the active session |
| `Ctrl+Enter` | Finish the active session (Running Task Bar or compact overlay) |
| `Ctrl+M` | Toggle compact overlay |
| `Ctrl+H` | Show or hide the main window |

Shortcuts are also listed in **Settings**.

### Typical workflow

1. **Capture** — type in Quick Capture or press `Ctrl+Shift+C` to add items to Inbox at tree root without interrupting your Running task.
2. **Organize** — drag tasks onto projects, expand/collapse the tree, convert tasks to projects when work grows, set optional estimates.
3. **Context** — select a project (or a task under a project) to edit context, deadline, and view effort rollup in the Context Panel.
4. **Execute** — double-click or start a task; use the **Running Task Bar** for timer, Done, Waiting, and Pause.
5. **Switch** — start another task (prior Running task returns to Ready by default), or use **Switch and mark waiting** when blocked.
6. **Review** — check focus time, heatmap, and streaks in **Analytics** (from Settings).

## Data and privacy

All data is stored locally in a SQLite database:

```text
%LocalAppData%\Jetset\jetset.db
```

Before schema upgrades, Jetset creates a timestamped backup alongside the database file (for example `jetset.db.backup-20260822120000`). No data is sent to external services by the application.

## Upgrading from V1

Existing Jetset databases upgrade automatically on first launch after installing V2. Historical sessions are linked to tasks by task name. Task statuses are remapped to the new lifecycle (for example, blocked tasks become Waiting). A welcome dialog introduces V2 navigation and keyboard shortcuts on first run.

**Note:** Primary navigation is **Work Tree** and **Settings**. Legacy Focus, Tasks, and Projects list views are no longer in the main nav — use the tree, Context Panel, and Running Task Bar instead. Press **Ctrl+M** for the compact timer overlay.

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
DOMAIN.md             Product domain specification
ARCHITECTURE.md       Technical architecture (ADR-0007 aligned)
ROADMAP.md            Slice-based delivery roadmap
IMPLEMENTATION_PLAN.md  Implementation plan v3.0
ADR-0007-worktree-workspace-n-unified-workitem-model.md  Workspace/UI authority
```

## Documentation

- [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md) — **Source of truth** for Work Tree workspace, WorkItem model, conversion, estimates, rollup
- [DOMAIN.md](./DOMAIN.md) — Product domain and design principles (subordinate to ADR-0007 for workspace/UI conflicts)
- [ARCHITECTURE.md](./ARCHITECTURE.md) — Technical architecture
- [ROADMAP.md](./ROADMAP.md) — Slice-based delivery roadmap
- [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) — Implementation plan v3.0 (supersedes v2.1 and V2-UI-IMPLEMENTATION-PLAN.md)

## License

This project is licensed under the [MIT License](./LICENSE.txt).
