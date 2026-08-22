# DOMAIN.md

Version: V2  
Product: Jetset  
Status: Approved for Implementation Planning

---

# 1. Product Overview

## Vision

Jetset is a **Personal Execution Workspace** for knowledge workers.

Jetset helps a single user:

- Capture work quickly
- Organize work with minimal friction
- Execute one task at a time
- Preserve project context across task switches
- Resume work without mental reload

Jetset is a personal productivity tool.

Jetset is **not**:

- Jira
- Asana
- ClickUp
- Trello
- Project management software

---

## Problem Statement

Knowledge workers often juggle many tasks and frequently lose context when switching between them.

When switching between tasks or projects, they frequently experience:

- Loss of context
- Forgotten progress
- Forgotten next actions
- Forgotten blockers
- Mental reload time

This context-switching cost reduces productivity.

Jetset helps users manage work, execute one task at a time, and preserve context so work can be resumed quickly.

---

## Scope

Jetset V2 is a **single-user** desktop application.

There are no:

- Teams
- Organizations
- Roles
- Permissions
- Shared workspaces

All data is local to the user's machine.

---

# 2. Design Principles

## Task First

Task is the primary execution unit.

Everything revolves around tasks. User activity centers on capturing, selecting, running, and completing tasks.

Projects exist only to group related tasks and hold shared working context (`ContextText`).

---

## Quick Capture

Users frequently receive interruptions and new work while already executing a task. Quick Capture is a **first-class capability**.

- Capture work to Inbox in seconds
- Must not disturb the currently Running task
- Keyboard-first and globally accessible (e.g., hotkey)
- No project setup required

Quick Capture is distinct from starting work. Capturing records the task; starting executes it.

---

## Single Active Task

A user may have many tasks.

However:

- **Only ONE task may be Running at any moment.**
- Starting another task automatically stops the previous Running task and returns it to an appropriate non-running state (typically Ready or Waiting, depending on user intent or prior state).

This is a fundamental, non-negotiable domain rule. The system must enforce it at all entry points.

---

## Minimal Friction

Creating a task should be extremely fast.

The system must never require unnecessary project setup.

Users must be able to capture work immediately.

---

## Project Optional

Tasks may belong to a project.

Tasks may also exist independently.

Both are first-class use cases.

Examples of independent tasks:

- Review email
- Fix SSL issue
- Call customer

Examples of project tasks:

- Implement repository layer
- Review architecture
- Remediate findings

---

## Context Preservation

Context preservation is one of the core value propositions.

However:

- **Context belongs to a Project, NOT to a Task.**
- A project contains a shared `ContextText` field — a free-form working note that survives task switching within that project.

The user may update project context at any time.

Context updates are **not** tied to:

- Starting a task
- Pausing a task
- Completing a task
- Work sessions

Context management and task execution are separate concerns.

---

# 3. Domain Model

The V2 domain is intentionally small. Three aggregates/entities form the core.

```
┌─────────────────────────────────────────┐
│              Project (optional)          │
│  ┌───────────────────────────────────┐  │
│  │         Project Context (ContextText) │  │
│  └───────────────────────────────────┘  │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐     │
│  │  Task   │ │  Task   │ │  Task   │ ... │
│  └─────────┘ └─────────┘ └─────────┘     │
└─────────────────────────────────────────┘

┌─────────┐  ┌─────────┐
│  Task   │  │  Task   │   (standalone; no project)
└─────────┘  └─────────┘
```

---

## 3.1 Project

**Aggregate root** for optional work grouping.

### Purpose

Group related tasks and hold shared working context that persists across task switches.

### Attributes

| Attribute | Type | Required | Notes |
|---|---|---|---|
| Id | Identifier | Yes | Stable identity |
| Name | String | Yes | Human-readable label |
| CreatedAt | Timestamp | Yes | Audit |
| UpdatedAt | Timestamp | Yes | Last modification |

Optional attributes (e.g., description, color, sort order) may be added during implementation if they support low-friction organization without introducing planning overhead.

### Attributes (context)

| Attribute | Type | Required | Notes |
|---|---|---|---|
| ContextText | Text | No | Free-form working note for the project |
| ContextUpdatedAt | Timestamp | No | Last context edit |

Context is a single editable text field — not a structured document. The purpose is context preservation, not structured project reporting.

### Contains

- Tasks (zero or more)
- Shared working context (`ContextText`)

### Business Rules

1. A project may exist with zero tasks.
2. A task may belong to at most one project at a time.
3. A task may be moved from one project to another.
4. A task may be detached from a project (becomes standalone).
5. Deleting a project must define behavior for contained tasks (recommended: detach tasks rather than delete them).
6. Context is updated independently of task status changes.
7. Context is **not** automatically captured or versioned on task lifecycle events.
8. Context history is **out of scope** for V2. Do not model snapshots, versions, or audit trails.
9. Standalone tasks (no project) have no project context. Resumption for standalone tasks relies on task title and status only.

---

## 3.2 Task

**Aggregate root** for work execution.

### Purpose

Represent a single unit of work the user can capture, organize, and execute.

### Attributes

| Attribute | Type | Required | Notes |
|---|---|---|---|
| Id | Identifier | Yes | Stable identity |
| Title | String | Yes | Short description of the work |
| Status | TaskStatus | Yes | Lifecycle state (see §4) |
| Origin | TaskOrigin | Yes | Planned or Unplanned |
| ProjectId | Identifier | No | Null when standalone |
| CreatedAt | Timestamp | Yes | When captured |
| CompletedAt | Timestamp | No | Set when status becomes Done |

Optional attributes (e.g., notes, sort order) may be added if they support capture and execution without reintroducing task-level context management.

### TaskOrigin

| Value | Meaning |
|---|---|
| Planned | Work intentionally created as part of planned execution |
| Unplanned | Unexpected work that appeared during the day |

Origin exists for **visibility only**. It must not introduce workflow complexity, gates, or different lifecycle rules.

### Business Rules

1. A task may exist without a project (`ProjectId` is null).
2. A task with a non-null `ProjectId` must reference an existing project.
3. Only one task may be in `Running` status globally (see §4.2).
4. A task in `Done` or `Cancelled` status is terminal for active execution (see §4.3).
5. `CompletedAt` is set when status transitions to `Done`; cleared if reopened (if reopening is supported).

---

## 3.3 Work Session (Supporting Entity)

**Entity** linked to a Task. Not a domain centerpiece.

### Purpose

Record focused work time spent on a task. Supports the existing session/timer engine.

### Attributes

| Attribute | Type | Required | Notes |
|---|---|---|---|
| Id | Identifier | Yes | |
| TaskId | Identifier | Yes | FK to Task |
| Mode | TimerMode | Yes | Stopwatch or Countdown |
| StartedAt | Timestamp | Yes | |
| FinishedAt | Timestamp | No | Null while active |
| State | SessionState | Yes | Running, Paused, Completed, Cancelled |
| ActiveDuration | Duration | Yes | Sum of focused intervals (pause-aware) |

Additional session fields (countdown duration, notes, intervals) may follow the existing V1 session model.

### Business Rules

1. Work sessions belong to exactly one task.
2. A work session may be active (Running or Paused) only while its task is `Running`.
3. Starting a task should start or resume a work session on that task (implementation detail; the invariant is temporal alignment).
4. Stopping or switching tasks should end or pause the active session on the previous task.
5. Time tracking supports execution; it does not drive task lifecycle or context management.

---

# 4. Task Lifecycle

## 4.1 States

| State | Meaning |
|---|---|
| **Inbox** | Captured but not yet organized. Default for quick capture. |
| **Ready** | Available to work on. |
| **Running** | Currently being worked on. At most one globally. |
| **Waiting** | Blocked by external dependency or awaiting response. |
| **Done** | Completed. |
| **Cancelled** | No longer relevant. |

```
                    ┌──────────┐
         capture ──►│  Inbox   │
                    └────┬─────┘
                         │ organize
                         ▼
                    ┌──────────┐     block      ┌──────────┐
              ┌────►│  Ready   │───────────────►│ Waiting  │
              │     └────┬─────┘                └────┬─────┘
              │          │ start                       │ unblocked
              │          ▼                             │
              │     ┌──────────┐◄──────────────────────┘
              │     │ Running  │  (only one globally)
              │     └────┬─────┘
              │          │ complete / cancel
              │          ▼
              │   ┌──────────┐   ┌───────────┐
              └───│   Done   │   │ Cancelled │
                  └──────────┘   └───────────┘
```

Transitions not shown (e.g., Inbox → Running for fast capture, Ready → Cancelled, Done → Ready if reopening is supported) are implementation decisions. Any transition that results in a new Running task must enforce the single-active-task rule.

---

## 4.2 Single Active Task Rule

**Invariant:** At most one task has status `Running` at any time.

### Enforcement

When a task transitions to `Running`:

1. If another task is currently `Running`, that task must leave `Running` first.
2. The previously Running task returns to an appropriate non-running state (see **Task switching behavior** below).
3. Any active work session on the previously Running task must be paused or completed.

### Task switching behavior

When starting Task B while Task A is Running, the system must resolve Task A's new status.

| Scenario | Task A becomes | When |
|---|---|---|
| **Default — switch focus** | `Ready` | User starts another task without indicating a block |
| **User marks blocked** | `Waiting` | User explicitly marks Task A as waiting/blocked before or during the switch |
| **Preserve prior state** | `Waiting` | Task A was `Waiting` before a brief resume; return to `Waiting` |
| **Quick capture only** | unchanged | User captures a new task to Inbox without starting it; Running task unaffected |

**Default is `Ready`.** Do not force all interrupted tasks to `Waiting`. A temporary focus change is not a block.

The user must be able to mark a task as `Waiting` when execution is genuinely blocked (customer response, review result, external dependency, approval). This is a deliberate user action, not an automatic consequence of every switch.

**Pragmatic workflow:**

- Start Task B → Task A becomes `Ready` (default)
- "Switch and mark waiting" (or equivalent) → Task A becomes `Waiting`
- User may move Task A to `Waiting` before or after switching, via status change

### Entry Points

This rule must be enforced wherever a task can become Running:

- Explicit "Start" / "Run" action on a task
- Quick capture that immediately starts work
- Search result → start work
- Session start linked to a task
- Any bulk or automated operation

There are no exceptions. UI, services, and persistence must all respect this invariant.

---

## 4.3 Terminal States

| State | Terminal? | Notes |
|---|---|---|
| Done | Yes (for execution) | Task is complete. `CompletedAt` is set. |
| Cancelled | Yes (for execution) | Task is abandoned. No `CompletedAt`. |

Reopening a Done or Cancelled task (transition back to Ready or Inbox) is optional for V2. If supported, `CompletedAt` must be cleared.

---

# 5. Business Processes

## Process 1 — Quick Capture

**Goal:** Capture work in seconds without losing focus on the current task.

Quick Capture is a **first-class capability**. Users frequently receive interruptions and new work while already executing another task. The system must let them record new work immediately without disturbing the Running task.

1. User enters a task title (keyboard-first, global hotkey, or one-click Inbox capture).
2. System creates a task with status `Inbox`.
3. The currently Running task (if any) **remains Running**. Quick capture does not switch execution.
4. Project assignment is optional and may be skipped.
5. Origin defaults to `Unplanned`.

The user may optionally start the captured task immediately (transitions to `Running` and triggers task switching rules).

**Outcome:** Work is recorded without setup friction and without breaking current focus.

---

## Process 2 — Organize Work

**Goal:** Move captured work into an actionable state.

1. User reviews Inbox tasks.
2. User may:
   - Move task to `Ready`
   - Assign or reassign to a project
   - Detach from a project
   - Set origin to `Planned` or `Unplanned`
   - Cancel irrelevant tasks
3. User may create a project (optional) and assign related tasks.

**Outcome:** Tasks are organized without mandatory project hierarchy.

---

## Process 3 — Start Work (Execute)

**Goal:** Focus on one task.

1. User selects a task and starts it.
2. System transitions task to `Running`.
3. System enforces single-active-task rule (stops any other Running task).
4. System starts or resumes a work session on the task (if time tracking is active).
5. If the task belongs to a project, the UI surfaces that project's context for reference (read-only display unless user edits).

**Outcome:** User is executing exactly one task with project context visible when relevant.

---

## Process 4 — Switch Work

**Goal:** Change focus without losing project context.

1. User starts a different task (not a quick capture).
2. System stops the previously Running task using task switching behavior (§4.2): default → `Ready`; user-initiated block → `Waiting`.
3. System pauses or completes the prior task's work session.
4. System starts the new task (`Running`) and its session.
5. Project context for the new task's project (if any) is displayed. Context was never tied to the task switch event.

**Outcome:** Focus moves cleanly. Project context persists on the project, not on individual tasks.

---

## Process 5 — Block Work

**Goal:** Mark a task as externally blocked.

1. User moves a Running or Ready task to `Waiting`.
2. If the task was Running, single-active-task rule releases the Running slot.
3. User may update the project's context (e.g., record the blocker) — this is a separate, optional action.

**Outcome:** Blocked work is visible; execution slot is freed.

---

## Process 6 — Complete Work

**Goal:** Finish a task.

1. User marks a task as `Done`.
2. System sets `CompletedAt`.
3. If the task was Running, the Running slot is released and its session is completed.
4. Project context is **not** auto-updated. User may edit project context separately if desired.

**Outcome:** Task is closed. Project context remains as the user left it.

---

## Process 7 — Maintain Project Context

**Goal:** Preserve project-level working state.

1. User opens a project.
2. User views or edits the project's `ContextText` (free-form working note).
3. System saves context with `ContextUpdatedAt`.

This process is independent of task lifecycle. It may happen before, during, or after any task operation.

**Outcome:** Project context reflects the user's current understanding of the project.

---

## Process 8 — Review Personal Productivity (Optional)

**Goal:** Lightweight self-awareness, not management reporting.

1. User views minimal analytics (see §7).
2. User gains awareness of focus patterns.

**Outcome:** Insight without overhead.

---

# 6. Business Rules Summary

| # | Rule | Severity |
|---|---|---|
| BR-1 | At most one task may be `Running` at any time | Invariant |
| BR-2 | Starting a task stops any other Running task | Invariant |
| BR-3 | Context belongs to Project, not Task | Domain |
| BR-4 | Context updates are independent of task lifecycle events | Domain |
| BR-5 | Tasks may exist without a project | Domain |
| BR-6 | Task origin (Planned/Unplanned) does not affect lifecycle | Domain |
| BR-7 | A project has a single `ContextText` field (not structured sub-fields) | Domain |
| BR-11 | Quick capture to Inbox does not change the Running task | Domain |
| BR-8 | Work sessions belong to tasks, not projects | Domain |
| BR-9 | Work sessions are active only while their task is Running | Invariant |
| BR-10 | Deleting a project should detach tasks, not cascade-delete them | Policy |

---

# 7. Supporting Capabilities

## 7.1 Time Tracking (Session Engine)

**Position:** Task execution is primary. Time tracking is a **supporting capability**.

The session engine exists to measure focused work time on a Running task. It is not a co-equal pillar of the product. Users come to Jetset to capture, organize, and execute tasks — not to manage timers.

The session engine exists to:

- Measure focused work duration on a task
- Support stopwatch and countdown modes
- Provide pause-aware active duration (inherited from V1)

It does **not**:

- Drive task lifecycle (sessions follow task execution, not the reverse)
- Trigger context capture
- Define productivity scores or coaching
- Require timer interaction to execute work (starting a task may start a session, but task status is authoritative)

The existing V1 session engine (interval-based duration, idle auto-pause, crash recovery) is a valuable implementation asset to **retain and simplify**, not to extend. Adapt it to reference `TaskId` and align with the single Running task rule. Deprioritize session-centric concepts (parallel paused sessions, resume-from-session queues) in favor of task-status-driven workflows.

---

## 7.2 Analytics

Analytics are **minimal** and personal.

### In Scope

| Metric | Purpose |
|---|---|
| Focus time (daily / per-task) | How much focused work happened |
| Session history | Review what was worked on |
| Activity heatmap (simple) | Visual consistency awareness |
| Productive streak (simple) | Gentle consistency motivation |

### Out of Scope

| Removed Concept | Reason |
|---|---|
| Project Momentum | Management-style trend reporting |
| Context Switch Metrics | Implies optimization coaching |
| WIP Health | Portfolio management concept |
| Productivity Coaching | Not a personal execution workspace feature |
| Goal Management | Removed from V2 |
| Context Reload Score | Removed from V2 |
| Context Freshness | Removed from V2 |

Analytics must not require milestones, subtasks, or task-level context to function.

---

# 8. Explicitly Removed Concepts

The following concepts from earlier drafts or V1 planning are **removed** from V2. Do not model, implement, or reference them in V2 work.

| Concept | Replacement / Rationale |
|---|---|
| Milestone | Projects group tasks directly. No intermediate planning layer. |
| Milestone Progress | No milestones. Task Done counts if needed are per-project, not per-milestone. |
| Subtask | Tasks are atomic. Large work is multiple tasks. |
| Context Snapshot | Project `ContextText` is a single editable note, not point-in-time captures. |
| Structured project context fields | Replaced by single `ContextText` on Project. |
| Task-level context fields | Context lives on Project only. |
| Resume Queue | User picks next task from Ready/Inbox/Search. No ordered queue. |
| Project Momentum | Removed analytics. |
| Context Freshness | Removed metric. |
| Context Reload Score | Removed metric. |
| Goal Management | Out of scope. |
| Productivity Coaching | Out of scope. |
| WIP Health | Out of scope. |
| AI Assistant | Out of scope. |
| Deadline (as domain entity) | Optional project metadata at most; not a planning driver. |
| Active / Blocked (old statuses) | Replaced by Inbox / Ready / Running / Waiting / Done / Cancelled. |
| Parallel active tasks | Replaced by single Running task; multiple tasks may be Waiting or Ready. |

---

# 9. Out of Scope (V2)

The following are intentionally excluded from V2:

- Multi-user / collaboration
- Cloud sync
- Context history / versioning
- Resume recommendation engine
- Stale task detection
- Focus capacity monitoring
- Habit management
- AI features
- Import/export (unless required for migration)
- Mobile or web clients

These may be considered in future versions.

---

# 10. Success Criteria

Jetset V2 is successful when a user can:

1. Capture a task in seconds without creating a project — including while another task is Running.
2. Organize tasks with optional project grouping.
3. Execute exactly one task at a time with confidence the system enforces focus.
4. Switch tasks without losing project-level context.
5. Edit project context at any time, independent of task operations.
6. Resume project work by reading project context, not reconstructing it from task history.
7. Track focused work time as a secondary benefit, not a primary workflow.
8. Glance at simple personal analytics without management-style reporting.

---

# 11. Architectural Implications

This section summarizes domain decisions that materially affect implementation planning, database design, UI design, and workflow behavior. Future implementation agents should treat this as a guardrail against reintroducing removed concepts.

## 11.1 Aggregate Boundaries

| Aggregate | Root | Children | Consistency Boundary |
|---|---|---|---|
| Project | Project | Task (by reference) | `ContextText` and project metadata are consistent within one project. |
| Task | Task | WorkSession(s) | Task status and session state must honor single-active-task and session-task alignment rules. |

Tasks are their own aggregate roots, not entities nested inside Project. `ProjectId` on Task is a reference, not an ownership hierarchy. This allows standalone tasks and cross-project moves without loading the entire project aggregate.

`ContextText` is embedded on the Project aggregate — not a separate entity or table.

## 11.2 Single Active Task — Implementation

- **Database:** Enforce at the service layer. Optionally add a partial unique index on `Status = Running` (only one row) if the database supports it; service-layer enforcement is mandatory regardless.
- **Service layer:** All task status changes go through a single command handler (e.g., `StartTask`, `ChangeTaskStatus`) that checks and clears any existing Running task atomically.
- **UI:** Only one task shows a "running" indicator globally. Starting a new task does not ask "are you sure?" by default — it switches focus per the domain rule.
- **Session integration:** Sessions follow task execution. `TaskService.StartTask` is the authority; session start/pause is a side effect. Do not treat `SessionService` as a peer execution authority.
- **Tests:** Explicit test cases for: start when none running, start when another running (auto-stop), concurrent start race, session-task alignment.

## 11.3 Context Model — Implementation

- **Embed context on Project:** `ContextText` + `ContextUpdatedAt` columns on `Project` table. No separate `ProjectContext` table or structured sub-fields.
- **No automatic context writes** on pause, switch, or complete events. Remove any V1/V2-draft hooks that prompt for context on task lifecycle transitions.
- **UI:** Project detail view has an always-editable text area for `ContextText`. Task detail view does **not** have context fields.
- **Standalone tasks:** No context panel. Resumption relies on title, status, and optional task notes.
- **Do not build** context history, structured context fields, diffing, freshness scoring, or reload prompts.

## 11.4 Simplified Schema (Expected)

Core tables:

```
Project
  Id, Name, ContextText, ContextUpdatedAt,
  CreatedAt, UpdatedAt

Task
  Id, Title, Status, Origin, ProjectId (nullable FK),
  CreatedAt, CompletedAt

WorkSession
  Id, TaskId (FK), Mode, StartedAt, FinishedAt,
  State, ... (interval fields per V1)
```

No tables for: Milestone, Subtask, ContextSnapshot, ResumeQueue, Goal, ContextHistory.

## 11.5 UI Structure (Expected)

| Area | Purpose | Key Constraint |
|---|---|---|
| Capture / Inbox | Fast task entry; global hotkey | No project required; does not disturb Running task |
| Tasks | List, filter, status transitions | One Running indicator globally |
| Projects | Group view + context editor | Context is project-scoped |
| Focus / Timer | Session control linked to Running task | Session follows single-active-task |
| Analytics | Minimal personal metrics | No momentum/WIP/coaching views |

Do not build: milestone boards, subtask trees, resume queue panel, goal trackers, Gantt charts, kanban with WIP limits.

## 11.6 Task Lifecycle Migration

V1 used free-text `TaskName` on sessions. V2 uses a `Task` entity.

- Migration links historical sessions to tasks by name matching (existing plan).
- Old statuses (Active, Blocked) map to: Active → Ready, Blocked → Waiting.
- New statuses (Inbox, Running) are new. Default captured tasks to Inbox.

## 11.7 Planned vs Unplanned

- Store as an enum column on Task. Default: `Unplanned` for quick capture, `Planned` when created inside a project planning flow (exact default is a UX choice).
- No distinct workflows, filters are informational only.
- Do not build separate views or approval flows per origin.

## 11.8 What Not to Reintroduce

Implementation agents must reject proposals that:

1. Add milestones, subtasks, or task hierarchies "for organization"
2. Attach context fields to tasks or create context snapshots on pause/switch
3. Allow multiple simultaneous Running tasks "for flexibility"
4. Build a resume queue or recommendation engine
5. Add management analytics (momentum, WIP health, switch scoring)
6. Couple context updates to session or task lifecycle events
7. Require project creation before task capture
8. Model goals, habits, or coaching feedback loops

When in doubt, ask: "Does this help one person capture, focus on one task, and preserve project context?" If not, it is out of scope.

---

*End of DOMAIN.md V2*
