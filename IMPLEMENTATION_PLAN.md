# Jetset V2 Implementation Plan

**Version:** 3.0  
**Status:** Approved Artifact  
**Source of Truth:** [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md)  
**Domain Reference:** [DOMAIN.md](./DOMAIN.md) (subordinate to ADR-0007 for workspace/UI/model decisions)  
**Date:** 2026-08-22  
**Supersedes:** Implementation Plan v2.1, [V2-UI-IMPLEMENTATION-PLAN.md](./V2-UI-IMPLEMENTATION-PLAN.md)

---

## Executive Summary

Jetset V2 adopts a **Work Tree Workspace** as its primary interaction surface. ADR-0007 (Accepted) supersedes the prior Focus-centric direction. Projects and Tasks form a unified **WorkItem** conceptual model. The user organizes work in a hierarchical tree, edits project context in an adjacent panel, and executes via a **Running Task Bar**.

The codebase has already completed significant DOMAIN.md alignment: six-state task lifecycle, single Running task, project `ContextText`, analytics simplification, and schema cleanup (migrations 008–011). Remaining work targets ADR-0007 gaps: Work Tree UI, Task↔Project conversion, optional task estimates, effort rollup, drag-drop membership, and navigation rework.

### Strategic posture

| Principle | Implication |
|---|---|
| ADR-0007 is source of truth | Focus Workspace artifacts are obsolete |
| Codebase is starting material | Reuse session engine, migrations, MVVM shell |
| Refactor over rewrite | Extend `TaskService`, `ProjectService`; new `WorkTreeService` |
| Option A hierarchy | Project → Task only; no nested projects in V2 |
| Incremental delivery | Each slice is shippable and independently testable |

---

# Phase 1 — Artifact Alignment Report

## Aligned

| Artifact | Section | Alignment with ADR-0007 |
|---|---|---|
| **ADR-0007** | Entire document | Source of truth |
| **DOMAIN.md** | §2 Quick Capture | Capture without disturbing Running task (BR-11 ↔ Decision 15) |
| **DOMAIN.md** | §2 Single Active Task | One Running task globally (↔ Decision 7) |
| **DOMAIN.md** | §2 Context Preservation | Context on Project, not Task (↔ Decisions 12–13) |
| **DOMAIN.md** | §3.1 Project `ContextText` | Project-owned editable context |
| **DOMAIN.md** | §3.3 Work Session | Sessions on tasks; effort from sessions (↔ Decision 8) |
| **DOMAIN.md** | §4 Task lifecycle | Inbox/Ready/Running/Waiting/Done/Cancelled |
| **DOMAIN.md** | §8 Removed concepts | Milestones, snapshots, resume queue, momentum |
| **DOMAIN.md** | §7.1 Session engine | Supporting capability; task execution primary |
| **DOMAIN.md** | §3.1 Project `Deadline` column | Deadline on projects exists in model |
| **Codebase** | `TaskService`, `ProjectService` | Lifecycle, `CaptureToInbox`, single Running, `ContextText` |
| **Codebase** | Migrations 008–011 | Status remap, context migration, schema cleanup |
| **Codebase** | `AppServices` | Milestones/snapshots/queue removed from wiring |

## Conflicting

| Artifact | Section | Conflict | ADR-0007 |
|---|---|---|---|
| **DOMAIN.md** | §2 "Task First" | Task is primary object | Work Tree is central object |
| **DOMAIN.md** | §11.5 UI Structure | Focus / Tasks / Projects / Analytics nav | Work Tree + Context Panel + Running Task Bar |
| **DOMAIN.md** | §8 Removed Concepts | "Deadline … not a planning driver" | Decision 11: deadline visible in workflow |
| **DOMAIN.md** | §3 Domain Model | Separate Project + Task aggregates | Unified WorkItem (`Task` + `Project`) |
| **DOMAIN.md** | Entire doc | No Task↔Project conversion | Decisions 2–3 |
| **DOMAIN.md** | Entire doc | Flat membership only | Hierarchical work tree + drag-drop (Decisions 4–5) |
| **DOMAIN.md** | Entire doc | No task estimate | Decision 9: optional estimate |
| **DOMAIN.md** | Entire doc | No effort rollup | Decision 10: derived rollup |
| **IMPLEMENTATION_PLAN.md** (v2.1) | Source of truth | DOMAIN.md | ADR-0007 supersedes for workspace/UI/model |
| **IMPLEMENTATION_PLAN.md** (v2.1) | Wave 4 R-09–R-11 | Focus/Tasks/Projects realignment | Obsolete direction |
| **V2-UI-IMPLEMENTATION-PLAN.md** | Entire document | Focus-centric UI | Work Tree primary layout |
| **README.md** | Features | Four primary areas: Focus, Tasks, Projects, Analytics | Work Tree Workspace primary |
| **Codebase** | `ShellArea`, `MainWindow` | Focus default nav tab | Work Tree should be default |
| **Codebase** | `FocusView` | Session/clock-centric layout | Not ADR layout |
| **Codebase** | Models | No `Estimate`; no conversion APIs | Decisions 2–3, 9–10 |

## Obsolete

| Artifact | Section | Reason |
|---|---|---|
| **IMPLEMENTATION_PLAN.md** (v2.1) | Wave 4 R-09 Focus realignment | Superseded by Work Tree UI |
| **IMPLEMENTATION_PLAN.md** (v2.1) | §6.6 Focus-centric UI table | Superseded by ADR Decision 14 |
| **V2-UI-IMPLEMENTATION-PLAN.md** | §2–5 Focus screen inventory & wireframe | Superseded by Work Tree + Context Panel |
| **V2-UI-IMPLEMENTATION-PLAN.md** | Slices 2–7 (Focus restructuring) | Replace with Work Tree slices |
| **DOMAIN.md** | §11.5 "Focus / Timer" row | Replace with Work Tree + Running Task Bar |
| **DOMAIN.md** | §8 "Deadline … not a planning driver" | ADR elevates deadline visibility |
| **Code** | `FocusView` as primary workspace | Retire or demote to compact overlay only |
| **Code** | `TasksView` / `ProjectsView` as co-primary nav | Demote to secondary or merge into tree |

**Missing artifacts (must be created):**

- `ARCHITECTURE.md` — referenced by ADR-0007 but does not exist
- `ROADMAP.md` — referenced by ADR-0007 but does not exist

---

# Phase 2 — Artifact Update Plan

## DOMAIN.md

**Current state:** Task-first design, separate Project/Task aggregates, Focus-centric UI expectations, deadline de-emphasized, no estimates/rollup/conversion.

**Required change:**

1. Add §2 design principle: **Work Tree First** — Work Tree is the primary interaction surface.
2. Replace §3 domain diagram with unified WorkItem model and tree hierarchy.
3. Add §3.4 WorkItem (conceptual union of Task + Project).
4. Add Task `Estimate` (optional `EstimateMinutes`).
5. Add conversion rules: Task→Project, Project→Task (`Children.Count == 0`).
6. Add effort rollup rules on Project (derived, not stored).
7. Update §8: remove "Deadline not a planning driver"; state deadline is project-only, workflow-visible.
8. Replace §11.5 UI with ADR Decision 14 layout.
9. Add drag-drop membership and expand/collapse as UI behaviors (not domain state).
10. Reconcile Quick Capture: ADR default `Parent=Root`; retain Inbox status per existing lifecycle.

**Reason:** ADR-0007 Decisions 1–15.

---

## ARCHITECTURE.md (create)

**Current state:** Does not exist.

**Required change:** Create document covering:

- Work Tree Workspace as primary UI architecture
- Unified WorkItem conceptual model with separate persistence (Task + Project tables)
- `WorkTreeService`, `WorkItemConversionService`, `EffortService`
- Context Panel resolution (selected project or owning project of selected task)
- Running Task Bar as execution chrome (not primary workspace)
- Tree expand/collapse persisted in UI layer (`AppSetting` or `TreeStateStore`)
- Session engine remains supporting layer
- Navigation: Work Tree (default) → Settings / Analytics (secondary)

**Reason:** ADR-0007 Required Follow-Up.

---

## ROADMAP.md (create)

**Current state:** Does not exist.

**Required change:** Create slice-based roadmap aligned to Phase 6 of this plan.

**Reason:** ADR-0007 Required Follow-Up.

---

## IMPLEMENTATION_PLAN.md (this document)

**Current state:** v2.1 DOMAIN.md-aligned Focus-centric plan.

**Required change:** Replaced by v3.0 (this document).

**Reason:** ADR-0007 supersedes Focus Workspace direction.

---

## V2-UI-IMPLEMENTATION-PLAN.md

**Current state:** Focus-centric UI plan.

**Required change:** Mark status **Superseded by IMPLEMENTATION_PLAN.md v3.0**.

**Reason:** Entire document conflicts with Decision 14.

---

## README.md

**Current state:** Describes Focus, Tasks, Projects, Analytics as four primary areas.

**Required change:**

1. Lead with **Work Tree Workspace** as primary surface.
2. Describe Context Panel, Running Task Bar, Quick Capture, drag-drop, conversion.
3. Demote Tasks/Projects/Focus to secondary or remove as primary nav concepts.
4. Add task estimates and project effort rollup to feature list.

**Reason:** ADR Decisions 4, 9, 10, 14, 15.

---

# Phase 3 — Domain Impact Analysis

## WorkItem Model

**Expected:**

```text
WorkItem
├── Task
└── Project
```

**Current state:** Separate `WorkTask` and `Project` entities with no shared abstraction or conversion.

**Evaluation:** Partially supports — both entities exist with `Id`, `Title`/`Name`, timestamps. No unified type, no polymorphic tree node, no conversion.

**Required changes:**

```csharp
// Conceptual — not prescriptive naming
public enum WorkItemKind { Task, Project }

public interface IWorkItemNode
{
    Guid Id { get; }
    WorkItemKind Kind { get; }
    string DisplayName { get; }
    Guid? ParentProjectId { get; }  // null = root
}
```

- `WorkTask` maps to `WorkItemKind.Task`
- `Project` maps to `WorkItemKind.Project` with `ParentProjectId = null` always (Option A)
- `WorkTreeService` builds tree from root projects + root tasks + children by `ProjectId`

---

## Task → Project Conversion

**Current state:** Not supported. `TaskService` and `ProjectService` are independent.

**Required changes:**

- `WorkItemConversionService.ConvertTaskToProject(taskId)`:
  1. Load task; reject if `Running`.
  2. Create `Project` with `Name = task.Title`.
  3. Delete original task (user adds child tasks manually after conversion).
  4. Return new project.

**Clarification:** ADR example implies user subsequently adds child tasks. Conversion does not auto-split title into subtasks.

---

## Project → Task Conversion

**Constraint:** `Project.Children.Count == 0`

**Current state:** Not supported. `ProjectService.Delete` detaches tasks; no conversion.

**Required changes:**

- `WorkItemConversionService.ConvertProjectToTask(projectId)`:
  1. Verify zero tasks with `ProjectId == projectId`.
  2. Create `WorkTask` with `Title = project.Name`, `Status = Ready` (or Inbox).
  3. Transfer `ContextText` to task `Notes` with user confirmation (context cannot live on task per ADR).
  4. Delete project; warn user about deadline loss.
  5. Return new task.

---

## Project Hierarchy

| Option | Structure | Assessment |
|---|---|---|
| **A** | `Project → Task` only | Matches ADR examples, drag-drop, rollup formula |
| **B** | `Project → Task + Project` | Not specified; requires `ParentProjectId`, recursive rollup |

**Recommendation: Option A for V2**

**Rationale:**

1. ADR Decision 5 describes Task→Project drag-drop, not Project→Project.
2. Rollup formula is `Sum(ChildTaskSpent)` — flat, not recursive.
3. Task→Project conversion produces a project container; nested projects add complexity without ADR mandate.
4. SIS example (`Student Lifecycle`, `Academic Delivery`) reads as tasks under a project.

**Future:** Option B can be a future ADR if needed; do not implement in V2.

---

## Effort Rollup

**ADR formulas:**

```text
ProjectSpent     = Sum(ChildTaskSpent)
ProjectEstimate  = Sum(ChildTaskEstimate)   // only tasks with estimates
```

**Current state:** No `Estimate` on tasks. Spent time computable via session aggregation.

**Recommendation:**

| Aspect | Approach |
|---|---|
| **Task spent** | `EffortService.GetTaskSpent(taskId)` — sum `WorkSession.ActiveDuration` |
| **Task estimate** | New `WorkTask.EstimateMinutes` (nullable `int?`) on Task table |
| **Project rollup** | Calculated on read in `EffortService.GetProjectRollup(projectId)` |
| **Persistence** | Do not store rollup on Project — derived only (ADR: no manual project effort) |
| **UI** | Context Panel and tree nodes show spent/estimate when present |
| **Performance** | Batch query for tree refresh; cache per refresh cycle in ViewModel |

**Edge cases:**

- Tasks without estimate contribute to spent sum but not estimate sum.
- Done/cancelled tasks still contribute spent.
- Standalone tasks show spent in tree; no rollup.

---

# Phase 4 — Architecture Impact Report

| Area | Impact | Rationale |
|---|---|---|
| **Entities** | **Major** | Add `Estimate` on Task; conceptual WorkItem; conversion transforms entities |
| **Aggregates** | **Major** | Unified WorkItem concept; conversion crosses aggregate boundaries |
| **Repositories** | **Minor** | Add tree queries (`ListRootItems`, `ListByParentProject`); existing stores sufficient |
| **Database Schema** | **Minor** | Add `Task.EstimateMinutes`; no `ParentProjectId` for Option A |
| **Services** | **Major** | New: `WorkTreeService`, `WorkItemConversionService`, `EffortService` |
| **Session Engine** | **No Change** | Spent derives from existing sessions |
| **Time Tracking** | **Minor** | Read-side aggregation for rollup |
| **Navigation** | **Major** | Work Tree default; Focus demoted |
| **ViewModels** | **Major** | New: `WorkTreeViewModel`, `ContextPanelViewModel`, `RunningTaskBarViewModel` |
| **Tree State Persistence** | **Minor** | New `ITreeStateStore` or `AppSetting` JSON; UI-only per Decision 6 |
| **Context Management** | **Minor** | Move from Focus inline to Context Panel |

---

# Phase 5 — UI Architecture

## Primary Layout (ADR Decision 14)

```text
┌──────────────────────────────────────────────────────────────┐
│ [Work Tree]  [Settings]  [Search…]                           │
├────────────────────────────┬─────────────────────────────────┤
│                            │                                 │
│  Work Tree                 │  Context Panel                  │
│  ─────────                 │  ─────────────                  │
│  [Quick Capture input]     │  Project: Jetset V2             │
│                            │  Deadline: 31 Dec 2026          │
│  ▼ Jetset V2  125h / 200h  │  Estimate: 200h (rollup)        │
│    Authentication  18h     │  Spent: 125h (rollup)           │
│    UI Design       40h     │                                 │
│  ▶ SIS                     │  Context:                       │
│  SSL Investigation  5h     │  ┌─────────────────────────┐   │
│                            │  │ editable ContextText    │   │
│                            │  └─────────────────────────┘   │
├────────────────────────────┴─────────────────────────────────┤
│ Running Task Bar                                             │
│ Authentication · Running · 01:24:32  [Done][Waiting][Pause]  │
└──────────────────────────────────────────────────────────────┘
```

## Screens

| Screen | Role | Primary? |
|---|---|---|
| **WorkTreeView** | Tree navigation, capture, drag-drop, expand/collapse | **Yes (default)** |
| **SettingsView** | Preferences, hotkeys, timer defaults | Secondary nav |
| **AnalyticsView** | Personal metrics | Secondary (from Settings) |
| **HistoryWindow** | Session history | Modal from Settings |
| **CompactOverlay** | Minimal Running Task Bar + timer | Mode toggle |
| **RecoveryDialog** | Crash recovery | Modal |

**Demoted / retire from primary nav:**

- `FocusView` → absorbed into Running Task Bar (+ optional compact overlay)
- `TasksView` → tree shows all tasks; keep as power-user secondary or remove
- `ProjectsView` → tree shows projects; Context Panel handles detail

## Navigation

```text
Startup → Work Tree

ShellArea: WorkTree, Settings   // Analytics via Settings

Cross-navigation:
  Search "Start"     → start task, stay on Work Tree
  Settings           → SettingsView
  Settings → Analytics → AnalyticsView (embedded or window)
```

## ViewModels

| ViewModel | Responsibility |
|---|---|
| `WorkTreeViewModel` | Tree nodes, selection, expand/collapse, quick capture, drag-drop |
| `WorkTreeNodeViewModel` | `Kind`, `Title`, `SpentText`, `EstimateText`, `IsExpanded`, `Children`, `IsRunning` |
| `ContextPanelViewModel` | Resolved project context, deadline edit, rollup display, `ContextText` edit |
| `RunningTaskBarViewModel` | Running task title, timer, Done/Waiting/Pause/Stop |
| `ShellViewModel` | Nav, window sizing (Work Tree ~720×560 default), search overlay |

**Context resolution:**

```text
Selected Task with ProjectId  → Context Panel shows owning project
Selected Project              → Context Panel shows that project
Selected standalone Task      → Context Panel hidden or minimal
Running task                  → Running Task Bar (independent of selection)
```

## Commands

| Command | Trigger | Behavior |
|---|---|---|
| `QuickCaptureCommand` | Capture input Enter / hotkey | Create Task, `Status=Inbox`, `Parent=Root`; Running unchanged |
| `StartTaskCommand` | Double-click task / Start button | `StartTask`; previous Running → Ready (default) |
| `MarkDoneCommand` | Running Task Bar | `CompleteTask` + end session |
| `MarkWaitingCommand` | Running Task Bar | `StopTask(Waiting)` + end session |
| `PauseResumeCommand` | Running Task Bar | Session pause/resume |
| `ToggleExpandCommand` | Click chevron | UI expand/collapse; persist state |
| `DragDropReparentCommand` | Drag task onto project | Set `Task.ProjectId`; drag to root → null |
| `ConvertToProjectCommand` | Context menu | `ConvertTaskToProject` |
| `ConvertToTaskCommand` | Context menu | `ConvertProjectToTask` (if no children) |
| `UpdateEstimateCommand` | Inline edit / panel | Set task estimate |
| `UpdateDeadlineCommand` | Context Panel | Set project deadline |
| `SaveContextCommand` | Context Panel debounce | `UpdateContextText` |

## Interactions

| Interaction | Behavior |
|---|---|
| **Quick Capture** | Always-visible input at top of tree; Enter creates root Inbox task |
| **Tree navigation** | Single selection drives Context Panel |
| **Drag & Drop** | Task→Project, Task→Root; no Project→Project in V2 |
| **Expand/Collapse** | Per-project; persisted in UI store |
| **Context editing** | Always editable in Context Panel when project resolved |
| **Deadline visibility** | Context Panel header when project selected |
| **Estimate visibility** | Tree node suffix + Context Panel rollup |
| **Effort visibility** | Tree node spent suffix; project rollup in panel |
| **Running Task Controls** | Bottom bar always visible when task Running |

---

# Phase 6 — Slice-Based Implementation Plan

Foundation note: lifecycle realignment, `ContextText`, schema cleanup (migrations 008–011) are largely complete. Slices below target ADR-0007 gaps.

---

## Slice 1 — ADR Alignment & Artifact Updates

**Goal:** All planning artifacts aligned to ADR-0007.

**Scope:** Update DOMAIN.md, README.md; create ARCHITECTURE.md, ROADMAP.md; supersede V2-UI-IMPLEMENTATION-PLAN.md.

**Domain Impact:** Documentation only.

**UI Impact:** None.

**Acceptance Criteria:**

- No artifact describes Focus as primary workspace.
- DOMAIN.md includes WorkItem, conversion, estimate, rollup, Work Tree UI.
- ARCHITECTURE.md and ROADMAP.md exist.

**Risks:** Low — documentation drift if not completed before code slices.

---

## Slice 2 — WorkItem Domain Foundation

**Goal:** Shared WorkItem concepts and effort primitives.

**Scope:** `WorkItemKind`, `IWorkItemNode`, `EffortService`, `WorkTreeService` (read-only tree queries).

**Domain Impact:** Add `EstimateMinutes` to `WorkTask`; migration 012.

**UI Impact:** None.

**Acceptance Criteria:**

- `ListRootWorkItems()` returns projects + standalone tasks.
- `GetChildren(projectId)` returns tasks.
- `GetTaskSpent` / `GetProjectRollup` return correct sums.
- Tests for rollup with mixed estimated/unestimated tasks.

**Risks:** Medium — migration for estimate column.

---

## Slice 3 — Task ↔ Project Conversion

**Goal:** Bidirectional conversion per ADR Decisions 2–3.

**Scope:** `WorkItemConversionService`, context menu command hooks.

**Domain Impact:** Conversion rules, validation (`Children.Count == 0`).

**UI Impact:** None yet (commands wired in Slice 7).

**Acceptance Criteria:**

- Task→Project creates project, removes task.
- Project→Task blocked when children exist.
- Running task cannot convert.
- Tests for all constraint paths.

**Risks:** Medium — context/deadline handling on Project→Task needs clear UX copy.

---

## Slice 4 — Tree State Persistence

**Goal:** Expand/collapse state survives restart.

**Scope:** `ITreeStateStore` (AppSetting JSON or small table).

**Domain Impact:** None (UI-only per ADR Decision 6).

**UI Impact:** Prerequisite for Slice 5.

**Acceptance Criteria:**

- Expanded project IDs persist and restore.
- No domain entity stores expansion state.

**Risks:** Low.

---

## Slice 5 — Work Tree UI Foundation

**Goal:** Primary workspace shell with split layout.

**Scope:** `WorkTreeView`, `ContextPanelView` (placeholder), `RunningTaskBarView`; `ShellArea.WorkTree` default; window sizing ~720×560.

**Domain Impact:** None.

**UI Impact:** **Major** — new primary view replaces Focus as startup.

**Acceptance Criteria:**

- App starts on Work Tree layout.
- Tree lists root items with children on expand.
- Selection changes Context Panel target.
- Running Task Bar visible at bottom.

**Risks:** High — navigation regression; mitigate with temporary Focus tab if needed (remove in Slice 12).

---

## Slice 6 — Drag & Drop Membership

**Goal:** Task→Project and Task→Root via drag-drop.

**Scope:** WPF drag-drop on `WorkTreeView`; `TaskService.AssignToProject` / `DetachFromProject`.

**Domain Impact:** Uses existing `ProjectId` FK.

**UI Impact:** Tree interaction.

**Acceptance Criteria:**

- Drag task onto project updates `ProjectId`.
- Drag to root clears `ProjectId`.
- Running task drag does not break session.
- Tree refresh reflects new structure.

**Risks:** Medium — WPF tree DnD complexity.

---

## Slice 7 — Context Panel

**Goal:** Full Context Panel per ADR Decisions 12–13.

**Scope:** `ContextPanelViewModel` — `ContextText` edit, deadline edit, rollup display, conversion context menus.

**Domain Impact:** Uses `ProjectService`, `EffortService`.

**UI Impact:** Right panel functional.

**Acceptance Criteria:**

- Selecting project or task-with-project shows context, deadline, rollup.
- Standalone task hides panel.
- Context edits persist independently of task ops.
- Convert to Project/Task available from context menu.

**Risks:** Low.

---

## Slice 8 — Deadline & Estimate Visibility

**Goal:** Surface estimates and deadlines in tree and panel.

**Scope:** Tree node templates show spent/estimate suffixes; inline estimate edit; deadline picker in Context Panel.

**Domain Impact:** Estimate CRUD on `TaskService`.

**UI Impact:** Tree + panel enrichment.

**Acceptance Criteria:**

- Task shows `18h / 12h` style when estimate set.
- Project shows rollup `125h / 200h`.
- Deadline visible on project in Context Panel.
- Tasks have no deadline field.

**Risks:** Low.

---

## Slice 9 — Running Task Bar

**Goal:** Execution chrome per ADR Decision 7.

**Scope:** Migrate timer + Done/Waiting/Pause from `FocusViewModel` to `RunningTaskBarViewModel`; bind to `GetRunningTask()` + session.

**Domain Impact:** None — uses existing `TaskService` / `WorkExecutionService`.

**UI Impact:** Bottom bar fully functional.

**Acceptance Criteria:**

- Only one Running task shown.
- Start task B auto-pauses A (no confirmation).
- Done/Waiting/Pause work from bar.
- Timer displays session active duration.

**Risks:** Medium — session/task binding regression.

---

## Slice 10 — Quick Capture Integration

**Goal:** Always-available capture per Decision 15.

**Scope:** Capture input in Work Tree header; global hotkey; `CaptureToInbox` with `Parent=Root`.

**Domain Impact:** None — BR-11 already enforced.

**UI Impact:** Capture input always visible.

**Acceptance Criteria:**

- Enter creates Inbox task at root.
- Running task unchanged after capture.
- Hotkey focuses capture from any view.

**Risks:** Low.

---

## Slice 11 — Navigation Cleanup & Secondary Views

**Goal:** Demote obsolete primary nav.

**Scope:** Remove or secondary-link Focus, Tasks, Projects tabs; Settings + Analytics secondary; update README/V2Welcome.

**Domain Impact:** None.

**UI Impact:** Nav finalization.

**Acceptance Criteria:**

- Primary nav: Work Tree + Settings.
- No duplicate task/project management as co-primary surfaces.
- `FocusView` removed or compact-only overlay.

**Risks:** Medium — user habit disruption.

---

## Slice 12 — Polish & Validation

**Goal:** Production-ready ADR-0007 workspace.

**Scope:** Compact overlay mode, visual polish, dead code removal, full test pass, migration upgrade path, success criteria walkthrough.

**Domain Impact:** None.

**UI Impact:** Polish.

**Acceptance Criteria:**

- `dotnet test` green.
- Upgrade from pre-012 DB succeeds.
- Manual walkthrough: capture → organize (drag) → convert → start → switch → complete → rollup correct.
- No forbidden concepts in UI (milestones, snapshots, queue, momentum).

**Risks:** Medium — integration regressions.

---

### Dependency Order

```text
Slice 1 (artifacts)
    ↓
Slice 2 → Slice 3 → Slice 4
    ↓         ↓
Slice 5 ──────┴── Slice 6, 7 (parallel after Slice 5)
    ↓
Slice 8, 9, 10 (parallel)
    ↓
Slice 11 → Slice 12
```

**Critical path:** 1 → 2 → 5 → 9 → 12

---

### Slice Summary

| Slice | Goal | Domain | UI | Risk |
|---|---|---|---|---|
| 1 | Artifact updates | Doc | — | Low |
| 2 | WorkItem foundation | Major | — | Medium |
| 3 | Task↔Project conversion | Major | — | Medium |
| 4 | Tree state persistence | — | Minor | Low |
| 5 | Work Tree UI foundation | — | **Major** | High |
| 6 | Drag & drop | Minor | Major | Medium |
| 7 | Context Panel | Minor | Major | Low |
| 8 | Deadline & estimate visibility | Minor | Major | Low |
| 9 | Running Task Bar | — | Major | Medium |
| 10 | Quick Capture | — | Major | Low |
| 11 | Navigation cleanup | — | Major | Medium |
| 12 | Polish & validation | — | Minor | Medium |

---

# Current State Assessment (Codebase)

## Technology Stack

| Layer | Technology | Status |
|---|---|---|
| Runtime | .NET 10 (`net10.0-windows`), WPF | Stable |
| UI pattern | MVVM (`ObservableObject`, `RelayCommand`) | Stable |
| Persistence | SQLite via `Microsoft.Data.Sqlite` | Stable |
| Schema evolution | Migrations 001–011 + backup + validation | Stable |
| Tests | xUnit | Good coverage |
| Composition | `AppServices.cs` single root | Stable |

## Completed Alignment (DOMAIN.md v2.1)

| Capability | Status |
|---|---|
| Six-state task lifecycle | ✅ Migrations 008 |
| Single Running task | ✅ `TaskService.StartTask` |
| `CaptureToInbox` (BR-11) | ✅ |
| Project `ContextText` | ✅ Migrations 009–010 |
| Schema cleanup (milestones, snapshots, switch events) | ✅ Migration 011 |
| Milestones/snapshots/queue removed from `AppServices` | ✅ |
| Analytics simplified | ✅ |

## Remaining Gaps (ADR-0007)

| Capability | Status |
|---|---|
| Work Tree Workspace UI | ❌ |
| Unified WorkItem model (conceptual) | ❌ |
| Task↔Project conversion | ❌ |
| Task estimate | ❌ |
| Effort rollup | ❌ |
| Drag-drop membership | ❌ |
| Tree expand/collapse persistence | ❌ |
| Context Panel layout | ❌ |
| Running Task Bar | ❌ (partially in FocusView) |
| Work Tree as default nav | ❌ |

## Target Schema (after Slice 2)

```sql
-- Existing (retained)
Project (Id, Name, Deadline, ContextText, ContextUpdatedAt,
         CreatedAt, UpdatedAt)

Task (Id, Title, Status, Origin, ProjectId NULL,
      CreatedAt, CompletedAt NULL, UpdatedAt, Notes NULL,
      EstimateMinutes NULL)   -- NEW in migration 012

WorkSession (unchanged)
WorkInterval (unchanged)
AppSetting (unchanged; may store tree expand state)
SchemaVersion (unchanged)
```

## Target Service Layer

| Service | Responsibility |
|---|---|
| `TaskService` | CRUD, search, `CaptureToInbox`, status transitions, `StartTask`/`StopTask`, estimate CRUD |
| `ProjectService` | Project CRUD, `ContextText`, deadline, delete-detaches-tasks |
| `SessionService` | Timer mechanics (supporting) |
| `WorkExecutionService` | Coordinates task status + session |
| `WorkTreeService` | **NEW** — tree queries, root items, children |
| `WorkItemConversionService` | **NEW** — Task↔Project conversion |
| `EffortService` | **NEW** — spent calculation, project rollup |
| `AnalyticsService` | Personal metrics (read-only, simplified) |

---

# Risks and Trade-offs

## Risk Matrix

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Work Tree UI regression during Slice 5 | High | Medium | Temporary dual-nav; incremental migration from FocusView |
| Session regression during Slice 9 | High | Medium | Keep `SessionService` unchanged; test `WorkExecutionService` every slice |
| WPF drag-drop complexity (Slice 6) | Medium | High | Prototype DnD early; fallback to context menu assign |
| Conversion data loss (context/deadline) | Medium | Medium | User confirmation dialogs; copy to Notes |
| Estimate migration (Slice 2) | Low | Low | Nullable column; no backfill required |
| User habit disruption (Slice 11) | Medium | Medium | Changelog; compact overlay preserves timer workflow |

## Trade-offs

| Decision | Trade-off | Why accepted |
|---|---|---|
| Option A hierarchy (no nested projects) | Cannot model sub-projects | ADR rollup formula is flat; simpler V2 |
| Derived rollup (not stored) | Recalculated on each tree refresh | ADR: no manual project effort; always consistent |
| Context → Notes on Project→Task conversion | Imperfect context preservation | ADR: context is project-only |
| Retain separate Task/Project tables | Not a single polymorphic table | Pragmatic; conceptual WorkItem union in services |
| Demote FocusView | Loses familiar entry point | ADR: Work Tree is primary; Running Task Bar retains execution |

## What We Are NOT Doing

- Nested projects (Option B hierarchy)
- Sprint management, milestones, kanban, WIP limits
- Context snapshots, resume queue, project momentum
- Multiple simultaneous Running tasks
- Manual project effort entry
- Task-level deadlines or context
- Rebuilding the session engine

---

# Success Validation

| # | Criterion | Validating Slices |
|---|---|---|
| 1 | Capture task instantly at root without disturbing Running task | 10 |
| 2 | Organize work in hierarchical tree via drag-drop | 5, 6 |
| 3 | Convert Task↔Project per ADR rules | 3, 7 |
| 4 | Execute exactly one task at a time | 9 |
| 5 | Project context accessible without navigation steps | 7 |
| 6 | Deadline and effort visible in normal workflow | 7, 8 |
| 7 | Optional task estimates with project rollup | 2, 8 |
| 8 | Time tracking as secondary benefit in Running Task Bar | 9 |

---

# Final Verdict

```text
GO WITH ARTIFACT UPDATES
```

**Rationale:**

1. ADR-0007 is accepted and authoritative — direction is clear.
2. Backend foundation (lifecycle, context, cleanup) is largely complete.
3. Artifacts and nav still describe Focus Workspace — Slice 1 must precede or parallel UI work.
4. No architectural blockers for Option A hierarchy and derived rollup.
5. Highest risk is Slice 5 (new primary UI) and Slice 9 (Running Task Bar migration).

## Recommended First Sprint

**Slice 1 + Slice 2 + Slice 5 (skeleton)**

1. Update artifacts so all agents reference Work Tree Workspace.
2. Add `EffortService`, estimate column, tree queries.
3. Ship Work Tree shell as default with read-only tree + placeholder panels.

---

## Related Artifacts

| Document | Role |
|---|---|
| [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md) | **Source of truth** for workspace/UI/model |
| [DOMAIN.md](./DOMAIN.md) | Product domain (subordinate to ADR-0007 for conflicts) |
| [README.md](./README.md) | User-facing docs (update in Slice 1/11) |
| ARCHITECTURE.md | To be created in Slice 1 |
| ROADMAP.md | To be created in Slice 1 |
| [V2-UI-IMPLEMENTATION-PLAN.md](./V2-UI-IMPLEMENTATION-PLAN.md) | **Superseded** by this plan |

---

*End of Implementation Plan v3.0*
