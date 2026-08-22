# ARCHITECTURE.md

**Version:** 1.0  
**Status:** Approved Artifact  
**Source of Truth:** [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md)  
**Domain Reference:** [DOMAIN.md](./DOMAIN.md)  
**Date:** 2026-08-22

---

## 1. Overview

Jetset V2 is a single-user Windows desktop application built on **.NET 10**, **WPF**, and **MVVM**. Data persists locally in **SQLite**. The primary interaction surface is the **Work Tree Workspace** — a split layout with tree navigation, project Context Panel, and Running Task Bar execution chrome.

ADR-0007 supersedes prior Focus-centric UI assumptions. This document describes the target architecture for ADR-aligned implementation.

---

## 2. Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (`net10.0-windows`), WPF |
| UI pattern | MVVM (`ObservableObject`, `RelayCommand`) |
| Persistence | SQLite via `Microsoft.Data.Sqlite` |
| Schema evolution | Numbered migrations + backup + validation |
| Tests | xUnit |
| Composition | `AppServices` — single service root |

---

## 3. UI Architecture

### 3.1 Primary Layout (ADR Decision 14)

```text
┌──────────────────────────────────────────────────────────────┐
│ [Work Tree]  [Settings]  [Search…]                           │
├────────────────────────────┬─────────────────────────────────┤
│  Work Tree                 │  Context Panel                  │
│  [Quick Capture input]     │  Project metadata + ContextText │
│  Hierarchical tree         │  Deadline, rollup, context edit │
├────────────────────────────┴─────────────────────────────────┤
│ Running Task Bar — Running task, timer, Done/Waiting/Pause   │
└──────────────────────────────────────────────────────────────┘
```

### 3.2 Screens

| Screen | Role | Primary? |
|---|---|---|
| `WorkTreeView` | Tree, capture, drag-drop, expand/collapse | **Yes (default)** |
| `SettingsView` | Preferences, hotkeys, timer defaults | Secondary |
| `AnalyticsView` | Personal metrics | Secondary (from Settings) |
| `HistoryWindow` | Session history | Modal from Settings |
| `CompactOverlay` | Minimal Running Task Bar + timer | Mode toggle |
| `RecoveryDialog` | Crash recovery | Modal |

**Demoted:** `FocusView` (absorbed into Running Task Bar), `TasksView` / `ProjectsView` (subsume into tree + panel or secondary power-user views).

### 3.3 Navigation

```text
Startup → Work Tree

ShellArea: WorkTree, Settings
Analytics → Settings → AnalyticsView
History → Settings → HistoryWindow
```

### 3.4 Context Panel Resolution

```text
Selected Task with ProjectId  → show owning project
Selected Project              → show that project
Selected standalone Task      → panel hidden or minimal
Running task                  → Running Task Bar (independent of selection)
```

### 3.5 Tree State Persistence

Expand/collapse per project is **UI-only** (ADR Decision 6). Persist via `ITreeStateStore` or `AppSetting` JSON. No domain entity stores expansion state.

---

## 4. Domain Architecture

### 4.1 Unified WorkItem (Conceptual)

Persistence remains separate `Project` and `Task` tables. Services expose a conceptual union:

```text
WorkItem
├── Task   (WorkTask entity)
└── Project (Project entity)
```

Hierarchy **Option A:** `Project → Task` only. No nested projects in V2. Projects appear at tree root; tasks appear at root (standalone) or as children of a project.

### 4.2 Aggregates

| Aggregate | Root | Consistency |
|---|---|---|
| Project | `Project` | `ContextText`, deadline, metadata; tasks referenced by `ProjectId` |
| Task | `WorkTask` | Status, estimate, sessions; single Running invariant |

Tasks are aggregate roots, not nested entities inside Project. `ProjectId` is a reference supporting moves and standalone tasks.

### 4.3 Effort Model

| Metric | Task | Project |
|---|---|---|
| Spent | Sum of `WorkSession.ActiveDuration` | `Sum(ChildTaskSpent)` — derived on read |
| Estimate | Optional `EstimateMinutes` | `Sum(ChildTaskEstimate)` — derived on read |

Rollup is never stored on Project. `EffortService` calculates on read.

### 4.4 Conversion

`WorkItemConversionService` crosses aggregate boundaries:

- **Task → Project:** Create project from task title; delete task; reject if Running.
- **Project → Task:** Create task from project name; delete project; reject if children exist; context → Notes with confirmation; warn on deadline loss.

---

## 5. Service Layer

| Service | Responsibility |
|---|---|
| `TaskService` | CRUD, search, `CaptureToInbox`, status transitions, `StartTask`/`StopTask`, estimate CRUD |
| `ProjectService` | Project CRUD, `ContextText`, deadline, delete-detaches-tasks |
| `SessionService` | Timer mechanics (supporting) |
| `WorkExecutionService` | Coordinates task status + session |
| `WorkTreeService` | Tree queries — root items, children by project |
| `WorkItemConversionService` | Task ↔ Project conversion |
| `EffortService` | Spent calculation, project rollup |
| `AnalyticsService` | Personal metrics (read-only, simplified) |

### 5.1 Session Engine

The session engine is a **supporting layer**. Task status (`TaskService.StartTask`) is the execution authority. Sessions follow task execution — not a peer authority.

Retain V1 interval-based duration, idle auto-pause, and crash recovery. Align with single Running task rule.

### 5.2 Single Active Task

All paths that set `Running` must go through `TaskService` (or `WorkExecutionService`) with atomic clear of any existing Running task. UI binds Running indicator to `GetRunningTask()`, not session idle state alone.

---

## 6. Persistence

### 6.1 Core Schema

```sql
Project (
  Id, Name, Deadline, ContextText, ContextUpdatedAt,
  CreatedAt, UpdatedAt
)

Task (
  Id, Title, Status, Origin, ProjectId NULL,
  EstimateMinutes NULL, Notes NULL,
  CreatedAt, CompletedAt NULL, UpdatedAt
)

WorkSession (unchanged)
WorkInterval (unchanged)
AppSetting (unchanged; may store tree expand state)
SchemaVersion (unchanged)
```

No tables for: Milestone, Subtask, ContextSnapshot, ResumeQueue, Goal.

### 6.2 Migrations

Schema evolves via numbered migrations (001–011 complete for lifecycle/context/cleanup). Migration 012 adds `Task.EstimateMinutes`.

Pre-upgrade backup: timestamped copy alongside `jetset.db` in `%LocalAppData%\Jetset\`.

---

## 7. ViewModels

| ViewModel | Responsibility |
|---|---|
| `WorkTreeViewModel` | Tree nodes, selection, expand/collapse, quick capture, drag-drop |
| `WorkTreeNodeViewModel` | Kind, title, spent/estimate text, expanded, children, running indicator |
| `ContextPanelViewModel` | Resolved project, deadline edit, rollup, `ContextText` edit |
| `RunningTaskBarViewModel` | Running task title, timer, Done/Waiting/Pause/Stop |
| `ShellViewModel` | Nav, window sizing (~720×560 default), search overlay |

---

## 8. Key Commands

| Command | Behavior |
|---|---|
| `QuickCaptureCommand` | Create Inbox task at root; Running unchanged |
| `StartTaskCommand` | Start task; prior Running → Ready (default) |
| `MarkDoneCommand` | Complete task + end session |
| `MarkWaitingCommand` | Stop task (Waiting) + end session |
| `PauseResumeCommand` | Session pause/resume |
| `ToggleExpandCommand` | UI expand/collapse; persist state |
| `DragDropReparentCommand` | Set or clear `Task.ProjectId` |
| `ConvertToProjectCommand` | `ConvertTaskToProject` |
| `ConvertToTaskCommand` | `ConvertProjectToTask` (no children) |
| `UpdateEstimateCommand` | Set task estimate |
| `UpdateDeadlineCommand` | Set project deadline |
| `SaveContextCommand` | Debounced `UpdateContextText` |

---

## 9. Explicit Non-Goals (V2)

- Nested projects (Option B hierarchy)
- Sprint management, milestones, kanban, WIP limits
- Context snapshots, resume queue, project momentum
- Multiple simultaneous Running tasks
- Manual project effort entry
- Task-level deadlines or context
- Rebuilding the session engine from scratch

---

## 10. Related Artifacts

| Document | Role |
|---|---|
| [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md) | Source of truth for workspace/UI/model |
| [DOMAIN.md](./DOMAIN.md) | Product domain |
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | Slice-based delivery plan v3.0 |
| [ROADMAP.md](./ROADMAP.md) | Slice roadmap |

---

*End of ARCHITECTURE.md*
