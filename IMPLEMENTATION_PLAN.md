# Jetset V2 Implementation Plan

**Version:** 2.1  
**Status:** Approved Artifact  
**Source of Truth:** [DOMAIN.md](./DOMAIN.md)  
**Date:** 2026-08-22  
**Supersedes:** Implementation Plan v2.0

---

## Executive Summary

The Jetset codebase has evolved beyond V1 into a **V2-draft productivity layer** that closely followed the old implementation plan. That plan treated Jetset as a lightweight project-management system: milestones, task-level context, context snapshots, resume queue, project momentum, and context-switch metrics.

The **approved DOMAIN.md** has since been rewritten. Jetset is now defined as a **Personal Execution Workspace** — task-first, single Running task, project-scoped context, minimal analytics.

**This plan does not preserve the old roadmap.** It evaluates the current codebase against DOMAIN.md and defines the shortest practical path to domain alignment.

### v2.1 refinements (2026-08-22)

| Decision | Change |
|---|---|
| **#1 Simplify project context** | `ContextText` on `Project` — no structured `ProjectContext` entity |
| **#2 Session engine position** | **Option B:** supporting capability; task execution is primary |
| **#3 Quick Capture** | First-class capability with dedicated roadmap slices |
| **#4 Task switching** | Default → `Ready`; user-initiated → `Waiting`; quick capture does not switch |

### Strategic posture

| Principle | Implication |
|---|---|
| DOMAIN.md is source of truth | Old slices S-03 through S-13, S-17, S-18 are **invalid** |
| Codebase is starting material | Reuse session engine, migrations, MVVM shell — not the draft domain model |
| Refactor over rewrite | Adapt `TaskService`, `WorkExecutionService`, views — do not rebuild from scratch |
| Simplification over expansion | Remove conflicting features before adding missing ones |
| Incremental delivery | Each wave is shippable and moves the product closer to DOMAIN.md |

---

# 1. Current State Assessment

## 1.1 Technology Stack

| Layer | Technology | Status |
|---|---|---|
| Runtime | .NET 10 (`net10.0-windows`), WPF | Stable |
| UI pattern | MVVM (`ObservableObject`, `RelayCommand`) | Stable |
| Persistence | SQLite via `Microsoft.Data.Sqlite` | Stable |
| Schema evolution | Versioned migrations 001–007 + backup + validation | Stable |
| Tests | xUnit — 12 test files | Good coverage of draft V2 features |
| Composition | `AppServices.cs` single root | Stable |

## 1.2 What Exists Today

### Session engine (V1 core — supporting capability)

`SessionService` provides stopwatch/countdown sessions, pause-aware `WorkInterval` duration, crash recovery, daily totals, and idle auto-pause. This is a **valuable implementation asset** but a **supporting capability** per DOMAIN.md §7.1 and approved design decision #2 (Option B).

Task execution (`TaskService`) is the primary authority. Sessions follow Running tasks — not the reverse. The session engine should be retained and simplified, not extended with new session-centric features (parallel paused sessions, resume-from-session queues).

Sessions are linked to tasks via `WorkSession.TaskId` (migration 006). Legacy `TaskName` column remains for backfill compatibility.

### Domain layer (V2-draft — partially misaligned)

| Component | Files | State |
|---|---|---|
| `WorkTask` | `Models/WorkTask.cs` | Exists; has task-level context fields + `MilestoneId` |
| `Project` | `Models/Project.cs` | Exists; has optional `Deadline` |
| `Milestone` | `Models/Milestone.cs` + store + service | **Conflicts with DOMAIN.md** |
| `ContextSnapshot` | Model + store + service | **Conflicts with DOMAIN.md** |
| `WorkingContext` | `Models/WorkingContext.cs` | Task-level; **conflicts** |
| `ResumeQueueEntry` | Model + `ResumeQueueService` | **Conflicts with DOMAIN.md** |
| `TaskSwitchEvent` | Model + store | **Conflicts with DOMAIN.md** |
| `TaskStatus` | `Active, Blocked, Done, Cancelled` | **Divergent** from DOMAIN.md |
| `TaskOrigin` | — | **Missing** |
| `ProjectContext` | — | **Not needed** — use `ContextText` on `Project` |

### Services

| Service | Alignment | Notes |
|---|---|---|
| `SessionService` | ✅ Keep (supporting) | Timer engine; simplify over time, do not extend |
| `TaskService` | ⚠️ Modify | CRUD works; wrong statuses, task context, milestone coupling; add Quick Capture + switching |
| `ProjectService` | ⚠️ Modify | CRUD works; add `ContextText`; simplify delete |
| `WorkExecutionService` | ⚠️ Modify | Orchestration is right pattern; snapshot/context capture is wrong |
| `MilestoneService` | ❌ Remove | Not in DOMAIN.md |
| `ContextSnapshotService` | ❌ Remove | Not in DOMAIN.md |
| `ResumeQueueService` | ❌ Remove | Not in DOMAIN.md |
| `AnalyticsService` | ⚠️ Simplify | Keep focus/heatmap/streak; remove momentum + switch metrics |
| `SettingsService`, `TrayService`, etc. | ✅ Keep | Desktop UX |

### Database (migrations 001–007)

```
SchemaVersion
WorkSession (+ TaskId)
WorkInterval
AppSetting
Task (+ task-level context columns, MilestoneId)
Project (+ Deadline)
Milestone                    ← remove
ContextSnapshot              ← remove
TaskSwitchEvent              ← remove
```

Missing: `Project.ContextText`, `Task.Origin`, `Task.CompletedAt`, `Task.Status` values for Inbox/Ready/Running/Waiting.

### UI (navigation shell exists)

| Area | View | Current behavior | Domain alignment |
|---|---|---|---|
| Focus | `FocusView` | Timer, task context panel, resume queue, start panel | Session UI good; context/queue wrong |
| Tasks | `TasksView` | Task CRUD, task context, snapshots, milestones | Needs lifecycle + removal of task context |
| Projects | `ProjectsView` | Milestones, momentum chart, task list | Needs project context panel; remove milestones/momentum |
| Analytics | `AnalyticsView` | Streak, heatmap, momentum, switch metrics | Keep streak/heatmap; remove momentum/switches |
| Modal | `ContextCaptureDialog` | Prompts on pause/switch/finish | **Remove** — violates BR-4 |
| Other | History, Settings, Recovery, V2Welcome | Functional | Update welcome copy after realignment |

### Test coverage

Tests exist for all draft V2 services including milestones, snapshots, resume queue, and analytics. Tests for removed features will be deleted or rewritten during realignment slices.

## 1.3 Current vs DOMAIN.md — At a Glance

```
DOMAIN.md target                    Current codebase
─────────────────                   ─────────────────
Task-first execution          →     Session-first execution
Single Running task           →     Multiple Active tasks + paused sessions
Context on Project            →     Context on Task + Snapshots
Inbox/Ready/Running/Waiting   →     Active/Blocked/Done/Cancelled
No milestones                 →     Full milestone stack
No resume queue               →     ResumeQueueService + Focus panel
Minimal analytics             →     + Project Momentum + Switch Metrics
Planned/Unplanned origin      →     Not implemented
Project ContextText           →     Not implemented
Quick Capture (first-class)   →     Partial (task create only; no hotkey/non-disrupting capture)
```

---

# 2. Domain Alignment Analysis

## 2.1 Aligned (keep as-is or minor adaptation)

| DOMAIN.md concept | Codebase evidence | Action |
|---|---|---|
| Single-user local desktop | WPF + SQLite, no auth | Keep |
| Task as execution unit | `WorkTask`, `TaskService`, task-linked sessions | Keep; extend lifecycle |
| Project optional | `Task.ProjectId` nullable | Keep |
| Work session on task | `WorkSession.TaskId`, migration 006 | Keep |
| Time tracking supporting | `SessionService`, intervals, idle pause | Keep |
| Navigation areas | `ShellArea`: Focus, Tasks, Projects, Analytics | Keep structure |
| Global search | `GlobalSearchViewModel` | Keep; search task title (+ project context later) |
| Focus time / daily summary | `AnalyticsService.GetDailySummary` | Keep |
| Activity heatmap | `AnalyticsService.GetActivityHeatmap` | Keep |
| Productive streak | `AnalyticsService.GetStreak` | Keep |
| Session history | `HistoryWindow` | Keep |
| Schema migrations | Migrations 001–007 | Keep; add realignment migrations |
| Project delete detaches tasks | `ProjectService.Delete` | Keep behavior |

## 2.2 Divergent (must change)

| DOMAIN.md rule | Current violation | Required change |
|---|---|---|
| BR-1/BR-2: Single Running task | Task status never `Running`; multiple Active tasks with paused sessions | Add `Running` status; enforce in `TaskService.StartTask()` |
| BR-3: Context on Project | Five context fields on `Task` table | Add `ContextText` on `Project`; remove task context columns |
| BR-4: Context independent of lifecycle | `ContextCaptureDialog` on pause/switch/finish; auto snapshots | Remove lifecycle hooks and dialog |
| BR-6: Origin visibility only | No `TaskOrigin` | Add enum column |
| §4.1: Six task states | Four states (`Active/Blocked`) | Migrate to `Inbox/Ready/Running/Waiting/Done/Cancelled` |
| §8: No milestones | Full milestone stack | Remove |
| §8: No context snapshots | `ContextSnapshot` table + service | Remove |
| §8: No resume queue | `ResumeQueueService` + Focus panel | Remove; replace with task status lists |
| §7.2: No momentum/switch metrics | Implemented in analytics | Remove |
| §3.3: `CompletedAt` | Uses `UpdatedAt` proxy | Add column |

## 2.3 Inverted architecture (root cause)

The draft V2 treated **sessions** as the execution primitive and **tasks** as metadata attached to sessions. DOMAIN.md treats **tasks** as the execution primitive with sessions as a supporting time record.

```
Current (wrong):                    Target (DOMAIN.md):

SessionService                      TaskService.StartTask()
  └─ one running session              └─ one Running task (BR-1)
  └─ multiple paused sessions           └─ triggers session start/pause
       └─ ResumeQueue derived                └─ previous task → Ready (default)
WorkExecutionService                    WorkExecutionService
  └─ preserves task context                 └─ coordinates task + session
  └─ captures snapshots                     └─ no context side effects
                                            Quick Capture → Inbox only (BR-11)
```

The realignment centers on making `TaskService` the authority for execution state, with `WorkExecutionService` coordinating sessions as a side effect.

---

# 3. Feature Retention Decisions

| Feature | Decision | Rationale |
|---|---|---|
| **Session engine** | ✅ Keep (supporting) | DOMAIN.md §7.1 Option B; retain V1 engine, simplify — do not extend |
| **Task CRUD** | ✅ Keep | Core domain; refactor status model |
| **Project CRUD** | ✅ Keep | Optional grouping per DOMAIN.md §3.1 |
| **WorkSession → TaskId** | ✅ Keep | Already implemented (M006) |
| **Navigation shell** | ✅ Keep | Focus/Tasks/Projects/Analytics maps to DOMAIN.md §11.5 |
| **Global search** | ✅ Keep | Process 3/4 entry point; retarget to task title + `ContextText` |
| **Quick Capture** | ✅ Keep + elevate | First-class capability (decision #3); `CaptureToInbox` without disturbing Running task |
| **Start work from task** | ✅ Keep | Process 3; wire to single Running task |
| **Daily focus time** | ✅ Keep | DOMAIN.md §7.2 in-scope |
| **Session history** | ✅ Keep | DOMAIN.md §7.2 in-scope |
| **Activity heatmap** | ✅ Keep | DOMAIN.md §7.2 in-scope |
| **Productive streak** | ✅ Keep | DOMAIN.md §7.2 in-scope |
| **Per-task focus breakdown** | ✅ Keep | Supports personal awareness |
| **Project optional deadline** | ⚠️ Keep as optional metadata | DOMAIN.md allows optional project attributes; not a planning driver — hide or de-emphasize in UI |
| **Task notes** | ⚠️ Keep (optional) | DOMAIN.md §3.3 allows optional notes; not context replacement |
| **Crash recovery** | ✅ Keep | Desktop reliability |
| **Idle auto-pause** | ✅ Keep | Session support |
| **V1 data backfill** | ✅ Keep | Migration 006 pattern; extend for status remap |

---

# 4. Feature Removal / Simplification Decisions

| Feature | Decision | Rationale | Removal scope |
|---|---|---|---|
| **Milestones** | ❌ Remove | DOMAIN.md §8 | Model, store, service, migration table, UI, tests, `Task.MilestoneId` |
| **Milestone progress** | ❌ Remove | Derived from milestones | `MilestoneProgress`, UI progress bars |
| **Subtasks** | — Not built | DOMAIN.md §8 | No action needed |
| **Context Snapshots** | ❌ Remove | DOMAIN.md §8; replaced by Project Context | Table, model, store, service, UI history, tests |
| **Task-level context fields** | ❌ Remove | BR-3; context is project-scoped | `CurrentStatus`, `LastProgress`, `NextAction`, `Blocker` on Task; `WorkingContext` model |
| **Context capture on lifecycle** | ❌ Remove | BR-4 | `ContextCaptureDialog`, `WorkExecutionService.PreserveContext`, ViewModel tests |
| **Resume Queue** | ❌ Remove | DOMAIN.md §8 | `ResumeQueueService`, `ResumeQueueEntry`, Focus panel, tests |
| **Project Momentum** | ❌ Remove | DOMAIN.md §7.2 out-of-scope | `GetProjectMomentum`, Analytics/Projects UI, `ProjectMomentumViewModels` |
| **Context Switch Metrics** | ❌ Remove | DOMAIN.md §7.2 out-of-scope | `TaskSwitchEvent` table, store, recording in `SessionService`, Analytics UI |
| **Resume Queue UI labels** | ❌ Remove | Cosmetic "Ready"/"Waiting" on queue items | Replaced by real task statuses |
| **V2 Welcome (draft copy)** | ⚠️ Rewrite | References milestones, snapshots, queue, momentum | Update after realignment |
| **README (draft copy)** | ⚠️ Rewrite | Same issue | Update after realignment |

### Simplification: Focus view "Waiting" panel

The current Focus view shows a **resume queue** of paused sessions. DOMAIN.md does not define this.

**Replace with:** A compact list of **Ready** and **Waiting** tasks (by task status), clickable to start work. Paused sessions remain an implementation detail of the session engine, not a user-facing queue concept.

### Simplification: Parallel paused sessions

The session engine allows multiple paused in-progress sessions. DOMAIN.md requires only one Running task. After realignment:

- Starting task B pauses task A's session AND sets task A to **Ready** by default (or **Waiting** if user explicitly marks blocked)
- Quick capture to Inbox does **not** change the Running task or its session
- Orphaned paused sessions without a Running task should be completable or discardable
- Long term: phase out multiple paused sessions across tasks; session engine serves the Running task only

---

# 5. Missing Capability Analysis

| DOMAIN.md capability | Current state | Gap |
|---|---|---|
| **Project ContextText** | Not implemented | Add `ContextText` + `ContextUpdatedAt` on `Project`; editable in project detail |
| **Task lifecycle (6 states)** | 4 wrong states | Enum remap + migration + UI filters/transitions |
| **Single Running task** | Session-only enforcement | `TaskService.StartTask` + `WorkExecutionService` coordination |
| **Task switching behavior** | Implicit Ready only | Default → Ready; explicit action → Waiting; preserve Waiting on re-switch |
| **TaskOrigin** | Missing | Enum + column + UI badge/filter |
| **CompletedAt** | Missing | Column; set on Done transition |
| **Quick Capture (first-class)** | Basic task create | `CaptureToInbox` API; global hotkey; non-disrupting capture while Running |
| **Inbox capture flow** | Tasks default to Active | Default new tasks to Inbox; add Inbox filter/view |
| **Organize work (Process 2)** | Partial | Inbox → Ready transitions; project assign/detach |
| **Project context on execute (Process 3)** | Shows task context | Show project `ContextText` when Running task has ProjectId |
| **Context edit independent (Process 7)** | N/A | Always-editable `ContextText` on project detail |
| **Search includes project context** | Searches task context fields | Retarget search to title + `ContextText` |

### Not missing (already sufficient)

- Session start/stop/pause from Focus view
- Task creation without project
- Project task grouping
- Basic analytics (focus, heatmap, streak)
- Schema migration infrastructure

---

# 6. Recommended Target Architecture

## 6.0 Session Engine Position (Approved Decision #2)

Before proceeding with implementation, the plan evaluates three positions for the session engine:

| Option | Description | Assessment |
|---|---|---|
| **A — Core capability** | Task execution and time tracking are equally important | **Rejected.** Positions the timer as co-equal with execution. Jetset is not a Pomodoro app. |
| **B — Supporting capability** | Task execution is primary; time tracking is secondary | **Selected.** Matches DOMAIN.md §7.1, success criterion #7, and product vision. |
| **C — Substantially simplified** | Strip down or replace the session engine | **Partially accepted** as a long-term direction within Option B. |

### Decision: Option B with gradual simplification

**Rationale:**

- DOMAIN.md defines task execution as the center; time tracking is §7.1 supporting capability
- Success criterion #7: "Track focused work time as a secondary benefit, not a primary workflow"
- The V1 session engine is a proven implementation asset (intervals, idle pause, recovery) — worth retaining
- The draft V2 over-indexed on session-centric patterns (parallel paused sessions, resume queue, session-driven switching) that conflict with task-first execution

**Implementation implications:**

| Aspect | Direction |
|---|---|
| **Authority** | `TaskService` owns execution state. Sessions follow Running tasks. |
| **Retain** | Interval-based duration, stopwatch/countdown, idle auto-pause, crash recovery, history |
| **Simplify** | Phase out parallel paused sessions across tasks; one in-progress session per Running task |
| **Do not extend** | No new session-centric features (resume queue, switch event recording, session-driven task status) |
| **UI priority** | Focus view shows Running task first, timer second. Quick Capture is equally prominent. |
| **WorkExecutionService** | Thin coordinator: `StartTask` → session side effect. Not a peer domain authority. |

## 6.1 Domain model (target)

```
┌─────────────────────────────────────────────────────────┐
│                     Application                          │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │ TaskService │  │ProjectService│  │ SessionService │  │
│  │ (PRIMARY —  │  │ (+ContextText)│  │ (SUPPORTING —  │  │
│  │  execution) │  │              │  │  time tracking)│  │
│  └──────┬──────┘  └──────┬───────┘  └───────┬────────┘  │
│         │                │                   │           │
│         └────────┬───────┴───────────────────┘           │
│                  ▼                                       │
│         WorkExecutionService                             │
│         (coordinates task status + session)              │
│                  │                                       │
│         AnalyticsService (read-only, simplified)       │
└─────────────────────────────────────────────────────────┘
```

## 6.2 Aggregate boundaries (from DOMAIN.md §11.1)

| Aggregate | Root | Store | Key invariant |
|---|---|---|---|
| Task | `WorkTask` | `ITaskStore` | At most one `Running`; sessions align with Running |
| Project | `Project` | `IProjectStore` | `ContextText` on project; context edits independent of tasks |
| Work Session | `WorkSession` | `ISessionStore` | Active only while parent task is Running |

## 6.3 Target schema

```sql
-- Existing (retained, modified)
Project (Id, Name, ContextText, ContextUpdatedAt,
         CreatedAt, UpdatedAt)
  -- Deadline: keep column, de-emphasize in UI

Task (Id, Title, Status, Origin, ProjectId NULL,
      CreatedAt, CompletedAt NULL, UpdatedAt, Notes NULL)
  -- Removed: CurrentStatus, LastProgress, NextAction, Blocker,
  --           MilestoneId, LastWorkedAt (optional: keep LastWorkedAt for sort)

WorkSession (unchanged from M006)
WorkInterval (unchanged)
AppSetting (unchanged)
SchemaVersion (unchanged)

-- Removed tables
-- Milestone, ContextSnapshot, TaskSwitchEvent
```

## 6.4 Target enums

```csharp
public enum TaskStatus
{
    Inbox = 0,
    Ready = 1,
    Running = 2,
    Waiting = 3,
    Done = 4,
    Cancelled = 5
}

public enum TaskOrigin
{
    Unplanned = 0,
    Planned = 1
}
```

## 6.5 Service responsibilities (target)

| Service | Responsibility |
|---|---|
| `TaskService` | CRUD, search, **`CaptureToInbox`** (BR-11), status transitions, **`StartTask`/`StopTask`** with BR-1/BR-2 and switching behavior, `CompletedAt` |
| `ProjectService` | Project CRUD, `ContextText` get/update, delete-detaches-tasks |
| `SessionService` | Timer mechanics only (supporting); no task-switch event recording |
| `WorkExecutionService` | `StartWork` → `TaskService.StartTask` + session side effect; `CaptureToInbox` does not touch sessions |
| `AnalyticsService` | `GetDailySummary`, `GetFocusTime`, `GetActivityHeatmap`, `GetStreak`, `GetFocusTimeByTask` |

**Removed services:** `MilestoneService`, `ContextSnapshotService`, `ResumeQueueService`

## 6.6 UI structure (target)

| Area | Primary actions | Key UI elements |
|---|---|---|
| **Focus** | Execute, pause, finish, switch task, **quick capture** | Running task indicator; timer (secondary); `ContextText` sidebar; Ready/Waiting picker; global hotkey capture |
| **Tasks** | Capture, organize, transition status | Inbox filter; status badges; quick-add; assign/detach project; start work button |
| **Projects** | Group tasks, edit context | Project list; **`ContextText` editor**; task list (no milestones) |
| **Analytics** | Personal awareness | Streak, heatmap, daily focus, per-task breakdown |

**Removed UI:** milestone lists, snapshot history, context capture dialog, resume queue panel, momentum charts, switch metrics section.

## 6.7 Key orchestration flows (target)

### Quick Capture (does not switch execution)

```
User triggers Quick Capture (hotkey / one-click)
  │
  └─ TaskService.CaptureToInbox(title)
       ├─ creates task: Status=Inbox, Origin=Unplanned
       └─ Running task (if any) is UNCHANGED (BR-11)
```

### Start / Switch Work

```
User clicks "Start" on Task B
  │
  ├─ TaskService.StartTask(B, leavingStatus: Ready)    ← default
  │    ├─ if Task A is Running → A.Status = Ready
  │    └─ B.Status = Running
  │
  └─ WorkExecutionService
       ├─ pause session on A
       └─ start/resume session on B

User clicks "Switch and mark waiting" on Task B
  │
  ├─ TaskService.StartTask(B, leavingStatus: Waiting)  ← explicit
  │    └─ Task A.Status = Waiting
  │
  └─ (same session coordination)
```

### Edit project context

```
User edits ContextText on project
  │
  └─ ProjectService.UpdateContextText(projectId, text)
       └─ no task or session side effects
```

---

# 7. Migration Strategy

## 7.1 Approach

Continue the existing versioned migration pattern (001–007). Add **realignment migrations** 008+ that:

1. Add new structures before removing old ones
2. Backfill data with explicit mapping rules
3. Drop deprecated tables/columns only after code no longer references them
4. Run `MigrationValidationService` checks after each destructive step

**Do not** rewrite migrations 001–007 — they reflect shipped history. New migrations correct the domain.

## 7.2 Status mapping (migration 008)

| Old `TaskStatus` | New `TaskStatus` | Rule |
|---|---|---|
| `Active` (0) | `Ready` (1) | Default mapping |
| `Active` with paused in-progress session | `Ready` (1) | Session state preserved separately |
| `Active` with running session | `Running` (2) | At most one; if multiple, keep newest session's task as Running, others → Ready |
| `Blocked` (1) | `Waiting` (3) | Direct map |
| `Done` (2) | `Done` (4) | Set `CompletedAt` = `UpdatedAt` |
| `Cancelled` (3) | `Cancelled` (5) | Direct map |

New quick-capture tasks default to `Inbox` (0) after migration.

## 7.3 Context migration (migration 009)

For each project with tasks that have task-level context data:

1. Add `ContextText` and `ContextUpdatedAt` columns to `Project`
2. Concatenate task-level context from the most recently updated task in that project into `ContextText` (best-effort; user may edit after migration)
3. Drop task-level context columns in migration 010

Standalone tasks lose context fields (DOMAIN.md §3.1 rule 9: resumption relies on title/status).

## 7.4 Milestone migration (migration 010)

1. Set `Task.MilestoneId` to NULL for all tasks
2. Drop `Milestone` table
3. Drop `Task.MilestoneId` column

Tasks remain on their projects. No data loss for tasks themselves.

## 7.5 Snapshot and switch event migration (migration 011)

1. Drop `ContextSnapshot` table (historical snapshots are not in DOMAIN.md; acceptable data loss with pre-migration backup)
2. Drop `TaskSwitchEvent` table
3. Stop recording switch events in `SessionService`

## 7.6 Code migration order

```
Phase A: Add new (ContextText on Project, new statuses, Origin, CompletedAt)
Phase B: Switch code to new model (services, UI)
Phase C: Remove old code (milestone, snapshot, queue, task context)
Phase D: Drop old schema (migrations 010–011)
Phase E: Update docs (README, welcome dialog)
```

## 7.7 Backward compatibility

- Pre-migration DB backup already handled by `DatabaseBackupService`
- `WorkSession.TaskName` retained until verified; drop in final polish slice
- V1 users upgrading through 001–007 then 008+ get seamless transition
- No export/import required

---

# 8. Incremental Delivery Roadmap

The roadmap is ordered to **remove conflicting concepts early** (reducing maintenance burden) while **adding missing domain features** in dependency order.

```
Wave 0 ──► Stop the bleeding (remove draft-only features from active development)
Wave 1 ──► Domain core (task lifecycle + single Running task + Quick Capture)
Wave 2 ──► Remove conflicting features (milestones, snapshots, queue, switch metrics)
Wave 3 ──► Project ContextText (simple free-form context on project)
Wave 4 ──► Execution alignment + UI realignment (task ↔ session, switching, views)
Wave 5 ──► Analytics simplification
Wave 6 ──► Schema cleanup + polish
```

Each wave delivers a coherent, testable increment. Waves 1–3 are the critical path.

---

# 9. Slice / Wave Plan

---

## Wave 0: Freeze Draft Direction

**Goal:** Prevent further drift toward project-management features. No user-visible changes.

| Slice | Work | Acceptance |
|---|---|---|
| **R-00** | Mark old plan superseded (this document). Add `// DOMAIN-REALIGNMENT` comments on files slated for removal. No code behavior changes. | Team/agents reference DOMAIN.md + this plan only |

---

## Wave 1: Task Lifecycle Realignment

**Goal:** Replace task status model with DOMAIN.md six-state lifecycle. Establish single Running task invariant.

| Slice | Scope | DB | Backend | UI | Tests |
|---|---|---|---|---|---|
| **R-01** | New `TaskStatus` enum values; add `Origin`, `CompletedAt` columns; migration 008 with status remap; `StartTask` with switching behavior (`leavingStatus` param, default `Ready`) | `ALTER Task` add columns; remap Status integers | Update `TaskStatusRules`; `TaskService.ChangeStatus`, `TaskService.StartTask` (BR-1/BR-2 + switching), `TaskService.CompleteTask`; remove milestone references | Status picker shows 6 states; Inbox filter on Tasks view | Rewrite `TaskServiceTests` for lifecycle + single Running + switching |
| **R-02** | Default capture to Inbox; Planned/Unplanned origin; **`CaptureToInbox`** API (BR-11) | None (columns from R-01) | `TaskService.CaptureToInbox(title)` — creates Inbox task, does not change Running task; `Create` defaults: `Inbox`, `Unplanned` | Origin badge on task list; optional filter | Tests: capture does not disturb Running task |
| **R-02b** | Quick Capture UX | None | Wire `CaptureToInbox` to shell | Global hotkey; keyboard-first capture from any view; one-click Inbox add on Focus/Tasks; minimal dialog (title only) | Manual: capture while Running task stays Running |

**Wave 1 outcome:** Tasks use correct lifecycle. Only one Running task. Quick Capture works without breaking focus.

**Dependencies:** None beyond current codebase.

---

## Wave 1b: Quick Capture UX (can overlap Wave 2)

**Goal:** Make Quick Capture a visible, first-class workflow.

| Slice | Scope | Acceptance |
|---|---|---|
| **R-02b** | (see Wave 1 above) | User can capture to Inbox via hotkey from any view without losing Running task |

*Note: R-02b is listed in Wave 1 for dependency clarity; may ship alongside Wave 2 removals.*

---

## Wave 2: Remove Conflicting Features (Backend)

**Goal:** Delete domain concepts that conflict with DOMAIN.md before building replacements.

| Slice | Scope | Remove | Keep |
|---|---|---|---|
| **R-03** | Remove milestones | `MilestoneService`, `IMilestoneStore`, `MilestoneStore`, `InMemoryMilestoneStore`, `Milestone` model, `MilestoneProgress`, `MilestoneListItemViewModel`, milestone UI, `MilestoneServiceTests`, `Task.MilestoneId` usage | Project and task assignment |
| **R-04** | Remove context snapshots + task context | `ContextSnapshotService`, stores, model, `WorkingContext`, task context columns usage, `ContextCaptureDialog`, `ContextCaptureViewModel`, `PreserveContext` in `WorkExecutionService`, snapshot UI, related tests | `Task.Notes` (optional simple notes) |
| **R-05** | Remove resume queue + switch metrics | `ResumeQueueService`, `ResumeQueueEntry`, `ResumeQueueItemViewModel`, `TaskSwitchEvent` store/model, switch recording in `SessionService`, `ResumeQueueServiceTests` | Session engine pause/switch mechanics |

**Wave 2 outcome:** Codebase no longer contains removed DOMAIN.md concepts. Focus view resume queue panel is stubbed/empty until Wave 4.

**Note:** R-03 through R-05 can partially overlap if merge conflicts are managed. R-04 depends on R-03 completing milestone decoupling in `TaskService`.

---

## Wave 3: Project ContextText

**Goal:** Implement context preservation — simple free-form note on project, independent of task lifecycle.

| Slice | Scope | DB | Backend | UI | Tests |
|---|---|---|---|---|---|
| **R-06** | `ContextText` on Project | Migration 009: add `ContextText`, `ContextUpdatedAt` to `Project`; migration 010: concatenate task context → `ContextText`; drop task context columns | `ProjectService.GetContextText`, `UpdateContextText` | Project detail: single always-editable text area | Context CRUD tests |
| **R-07** | Context on execute | None | `FocusViewModel` loads `ContextText` when Running task has ProjectId | Focus view: read-only `ContextText` display (link to edit on project) | Integration test: start task → context displayed |

**Wave 3 outcome:** Context preservation works per DOMAIN.md. No structured fields. No lifecycle-triggered capture.

---

## Wave 4: Execution Alignment

**Goal:** Unify task Running status with session state. Task switching with explicit Ready/Waiting behavior.

| Slice | Scope | Backend | UI | Tests |
|---|---|---|---|---|
| **R-08** | Refactor `WorkExecutionService` | All start/pause/finish/switch flows through `TaskService.StartTask` / `StopTask`; `leavingStatus` param (default Ready, explicit Waiting); session follows task; no context side effects | — | Rewrite `WorkExecutionServiceTests` incl. switching scenarios |
| **R-09** | Focus view realignment | — | Remove resume queue panel; add Ready/Waiting task list; single Running indicator; **Quick Capture** input; "Switch and mark waiting" action; remove task context panel | Manual + VM tests |
| **R-10** | Tasks view realignment | — | Remove snapshot history, task context fields, milestone picker; add Inbox/Ready/Waiting/Done filters; start work button uses `StartTask` | — |
| **R-11** | Projects view realignment | — | Remove milestones section and momentum chart; task list + `ContextText` editor is primary content | — |
| **R-12** | Search realignment | `TaskService.Search` includes `ContextText` | Search results show task status + project name | Search tests |

**Wave 4 outcome:** Full execution workflow matches DOMAIN.md Processes 1–7.

---

## Wave 5: Analytics Simplification

**Goal:** Keep personal productivity metrics; remove management-style reporting.

| Slice | Scope | Remove | Keep |
|---|---|---|---|
| **R-13** | Simplify `AnalyticsService` | `GetProjectMomentum`, `GetSwitchMetrics`, `TaskSwitchEvent` dependencies | `GetDailySummary`, `GetFocusTime`, `GetActivityHeatmap`, `GetStreak`, `GetFocusTimeByTask` |
| **R-14** | Simplify Analytics UI | Momentum section, switch metrics section, per-project momentum chart | Streak badge, heatmap, daily focus, per-task breakdown |
| **R-15** | Simplify Projects UI analytics | Project momentum chart on project detail (if not removed in R-11) | — |

**Wave 5 outcome:** Analytics matches DOMAIN.md §7.2.

---

## Wave 6: Schema Cleanup and Polish

**Goal:** Drop deprecated schema, update docs, verify V1 upgrade path.

| Slice | Scope | Acceptance |
|---|---|---|
| **R-16** | Schema cleanup migration 011 | Drop `Milestone`, `ContextSnapshot`, `TaskSwitchEvent` tables; drop `Task.MilestoneId`, task context columns; validation passes |
| **R-17** | Remove dead code | No references to removed types; `AppServices` wiring cleaned; all tests pass |
| **R-18** | Update README + V2Welcome | Docs describe Personal Execution Workspace, not project management |
| **R-19** | Migration validation | Test upgrade from V1 DB → current; test upgrade from draft V2 (001–007) → aligned schema; verify session data intact |

**Wave 6 outcome:** Production-ready aligned V2.

---

## Wave Summary

| Wave | Slices | User-visible outcome | Risk |
|---|---|---|---|
| 0 | R-00 | None (planning) | None |
| 1 | R-01, R-02, R-02b | Correct task states, single Running, Quick Capture | Medium — status migration |
| 2 | R-03, R-04, R-05 | Features disappear (milestones, snapshots, queue) | Low — removal |
| 3 | R-06, R-07 | Project `ContextText` editing and display | Low — simple model |
| 4 | R-08–R-12 | Execution workspace workflow + task switching | **High** — session integration |
| 5 | R-13–R-15 | Simpler analytics | Low |
| 6 | R-16–R-19 | Clean schema, updated docs | Medium — data migration |

---

## Critical Path

```
R-01 → R-02 → R-02b → R-04 → R-06 → R-08 → R-09 → R-16 → R-19
              ↓
             R-03 → R-05
```

Task lifecycle (R-01), Quick Capture (R-02/R-02b), and execution alignment (R-08) are the highest-risk integration points. Project `ContextText` (R-06) is low complexity once task context is removed (R-04).

---

## Recommended First Sprint

**R-01 + R-02 + R-03** (can parallelize R-03 after R-01 starts)

1. **R-01** — New task lifecycle + single Running task + switching behavior (establishes domain authority)
2. **R-02** — `CaptureToInbox` API + Inbox defaults (first-class capture backend)
3. **R-03** — Remove milestones (eliminates biggest project-management artifact)

Follow with **R-02b** (Quick Capture UX) and **R-05** (remove queue/switch metrics) in the next sprint.

---

# 10. Risks and Trade-offs

## 10.1 Risk matrix

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| **Session regression** during R-08 execution alignment | High | Medium | Keep `SessionService` internals unchanged; only change orchestration in `WorkExecutionService`; run full `SessionServiceTests` every slice |
| **Status migration data loss** (R-01 / M008) | High | Low | Explicit mapping table; pre-migration backup; validation: no task left with invalid status; at most one Running |
| **Context data loss** on task→`ContextText` migration (R-06) | Medium | Medium | Best-effort concatenation from most recent task; backup before migration; user edits post-upgrade |
| **Quick Capture hotkey conflicts** | Low | Medium | Configurable hotkey in Settings; default avoids common IDE shortcuts |
| **Task switching confusion** (Ready vs Waiting) | Medium | Medium | Default to Ready; explicit "mark waiting" action; clear status badges |
| **Removing features users may have adopted** | Medium | Medium | Draft V2 may not be widely deployed; changelog notes removals; backup protects snapshot/milestone data in DB file |
| **Parallel paused sessions vs single Running task** | Medium | High | R-08 defines behavior: switching tasks pauses prior session; consider auto-completing stale paused sessions in R-19 |
| **Large deletion diff** (R-03–R-05) | Low | High | Delete in dedicated slices; compile after each removal; avoid mixing with new features |
| **UI scope creep** during realignment | Medium | Medium | DOMAIN.md §11.8 guardrails; no new features until alignment complete |
| **Test suite churn** | Low | High | Expected; rewrite tests per slice, don't maintain tests for removed features |
| **Enum integer remap breaks existing DBs** | High | Medium | Migration 008 remaps in SQL; never reuse old integer values for different meanings without migration |

## 10.2 Trade-offs

| Decision | Trade-off | Why accepted |
|---|---|---|
| Delete milestone/snapshot data | Irreversible without backup | DOMAIN.md explicitly removes these concepts; backup preserves raw DB |
| Aggregate task context → `ContextText` | Imperfect merge into single text field | Better than losing all context; simpler than structured fields; user edits afterward |
| Phase removal before adding `ContextText` | Temporary loss of context UI | Prevents building on wrong model; shortest path to correct architecture |
| Session engine retained but demoted | Some legacy session patterns remain initially | Option B: retain proven engine, simplify incrementally in R-08/R-19 |
| No resume queue | Less guided "what's next" | DOMAIN.md: user picks from Ready/Inbox/Search/Quick Capture — simpler, less PM-like |
| Default switch → Ready, not Waiting | User must explicitly mark blocks | Avoids falsely marking interrupted work as blocked; matches decision #4 |

## 10.3 What we are NOT doing

- Rebuilding the session engine
- Adding subtasks, milestones, goals, or coaching "because they were planned before"
- Migrating context snapshots to project context history (no history in V2)
- Supporting multiple Running tasks "for power users"
- Building kanban boards, Gantt charts, or WIP limits
- Automatic context capture "temporarily until project context is ready"

## 10.4 Success validation (DOMAIN.md §10)

| # | Success criterion | Validating slices |
|---|---|---|
| 1 | Capture a task in seconds without project — while Running | R-02, R-02b |
| 2 | Organize tasks with optional project grouping | R-01, R-03, R-10, R-11 |
| 3 | Execute exactly one task at a time | R-01, R-08, R-09 |
| 4 | Switch tasks without losing project context | R-06, R-07, R-08 |
| 5 | Edit project context independent of task ops | R-06 |
| 6 | Resume project work via `ContextText` | R-06, R-07 |
| 7 | Time tracking as secondary benefit | R-08 (session follows task) |
| 8 | Simple personal analytics | R-13, R-14 |

---

## Related Artifacts

| Document | Role |
|---|---|
| [DOMAIN.md](./DOMAIN.md) | Product domain specification (**source of truth**) |
| [README.md](./README.md) | User-facing documentation (update in R-18) |
| **IMPLEMENTATION_PLAN.md** (this file) | Realignment implementation plan |

---

## Appendix A: File Impact Map

Quick reference for agents implementing slices.

### Delete (Wave 2)

```
Models/Milestone.cs, MilestoneProgress.cs
Models/ContextSnapshot.cs, WorkingContext.cs
Models/ResumeQueueEntry.cs, TaskSwitchEvent.cs
Services/MilestoneService.cs, ContextSnapshotService.cs, ResumeQueueService.cs
Persistence/IMilestoneStore.cs, MilestoneStore.cs, InMemoryMilestoneStore.cs
Persistence/IContextSnapshotStore.cs, ContextSnapshotStore.cs, InMemoryContextSnapshotStore.cs
Persistence/ITaskSwitchEventStore.cs, TaskSwitchEventStore.cs, InMemoryTaskSwitchEventStore.cs
Persistence/Migrations/Migration004_AddMilestoneTable.cs  (superseded by drop migration)
Persistence/Migrations/Migration005_AddContextSnapshotTable.cs
Persistence/Migrations/Migration007_AddTaskSwitchEventTable.cs
ViewModels/MilestoneListItemViewModel.cs, ContextSnapshotItemViewModel.cs
ViewModels/ResumeQueueItemViewModel.cs, ContextCaptureViewModel.cs
ViewModels/ProjectMomentumViewModels.cs
Views/ContextCaptureDialog.xaml(.cs)
tests: MilestoneServiceTests, ContextSnapshotServiceTests,
       ResumeQueueServiceTests, ContextCaptureViewModelTests
```

### Modify (Waves 1–5)

```
Models/TaskStatus.cs, TaskStatusRules.cs, WorkTask.cs
Models/Project.cs (+ ContextText on Project, not separate entity)
Services/TaskService.cs, ProjectService.cs, WorkExecutionService.cs
Services/SessionService.cs (remove switch event recording)
Services/AnalyticsService.cs, AppServices.cs
ViewModels/FocusViewModel.cs, TasksViewModel.cs, ProjectsViewModel.cs
ViewModels/AnalyticsViewModel.cs, GlobalSearchViewModel.cs, ShellViewModel.cs
Views/FocusView.xaml, TasksView.xaml, ProjectsView.xaml, AnalyticsView.xaml
Persistence/TaskStore.cs, ProjectStore.cs, ITaskStore.cs, IProjectStore.cs
```

### Add (Wave 3)

```
Persistence/Migrations/Migration008_TaskLifecycleRealignment.cs
Persistence/Migrations/Migration009_AddProjectContextText.cs
Persistence/Migrations/Migration010_DropMilestoneAndTaskContext.cs
Persistence/Migrations/Migration011_DropSnapshotAndSwitchEvents.cs
```

---

*End of Implementation Plan v2.1*
