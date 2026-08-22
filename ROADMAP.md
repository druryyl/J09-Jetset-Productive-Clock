# ROADMAP.md

**Version:** 1.0  
**Status:** Approved Artifact  
**Source of Truth:** [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) v3.0 (Phase 6)  
**ADR:** [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md)  
**Date:** 2026-08-22

---

## Overview

Jetset V2 delivery is organized into **12 incremental slices**. Each slice is independently shippable and testable. Foundation work (six-state lifecycle, `ContextText`, migrations 008–011) is largely complete. Remaining work targets ADR-0007 gaps.

**Critical path:** Slice 1 → 2 → 5 → 9 → 12

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

---

## Slice Status

| Slice | Goal | Domain | UI | Risk | Status |
|---|---|---|---|---|---|
| **1** | ADR alignment & artifact updates | Doc | — | Low | **Done** |
| **2** | WorkItem domain foundation | Major | — | Medium | Planned |
| **3** | Task ↔ Project conversion | Major | — | Medium | Planned |
| **4** | Tree state persistence | — | Minor | Low | Planned |
| **5** | Work Tree UI foundation | — | **Major** | High | Planned |
| **6** | Drag & drop membership | Minor | Major | Medium | Planned |
| **7** | Context Panel | Minor | Major | Low | Planned |
| **8** | Deadline & estimate visibility | Minor | Major | Low | Planned |
| **9** | Running Task Bar | — | Major | Medium | Planned |
| **10** | Quick Capture integration | — | Major | Low | Planned |
| **11** | Navigation cleanup | — | Major | Medium | Planned |
| **12** | Polish & validation | — | Minor | Medium | Planned |

---

## Slice 1 — ADR Alignment & Artifact Updates ✅

**Goal:** All planning artifacts aligned to ADR-0007.

**Deliverables:**

- [x] Update `DOMAIN.md` — WorkItem, conversion, estimate, rollup, Work Tree UI
- [x] Update `README.md` — Work Tree Workspace primary
- [x] Create `ARCHITECTURE.md`
- [x] Create `ROADMAP.md` (this document)
- [x] Supersede `V2-UI-IMPLEMENTATION-PLAN.md`

**Acceptance:** No artifact describes Focus as primary workspace; `ARCHITECTURE.md` and `ROADMAP.md` exist.

---

## Slice 2 — WorkItem Domain Foundation

**Goal:** Shared WorkItem concepts and effort primitives.

**Scope:** `WorkItemKind`, `IWorkItemNode`, `EffortService`, `WorkTreeService` (read-only tree queries).

**Domain:** Add `EstimateMinutes` to `WorkTask`; migration 012.

**Acceptance:**

- `ListRootWorkItems()` returns projects + standalone tasks
- `GetChildren(projectId)` returns tasks
- `GetTaskSpent` / `GetProjectRollup` return correct sums
- Tests for rollup with mixed estimated/unestimated tasks

---

## Slice 3 — Task ↔ Project Conversion

**Goal:** Bidirectional conversion per ADR Decisions 2–3.

**Scope:** `WorkItemConversionService`, context menu command hooks (UI in Slice 7).

**Acceptance:**

- Task→Project creates project, removes task
- Project→Task blocked when children exist
- Running task cannot convert
- Tests for all constraint paths

---

## Slice 4 — Tree State Persistence

**Goal:** Expand/collapse state survives restart.

**Scope:** `ITreeStateStore` (AppSetting JSON or small table).

**Acceptance:**

- Expanded project IDs persist and restore
- No domain entity stores expansion state

---

## Slice 5 — Work Tree UI Foundation

**Goal:** Primary workspace shell with split layout.

**Scope:** `WorkTreeView`, `ContextPanelView` (placeholder), `RunningTaskBarView`; `ShellArea.WorkTree` default; window ~720×560.

**Acceptance:**

- App starts on Work Tree layout
- Tree lists root items with children on expand
- Selection changes Context Panel target
- Running Task Bar visible at bottom

---

## Slice 6 — Drag & Drop Membership

**Goal:** Task→Project and Task→Root via drag-drop.

**Scope:** WPF drag-drop on `WorkTreeView`; `TaskService.AssignToProject` / `DetachFromProject`.

**Acceptance:**

- Drag task onto project updates `ProjectId`
- Drag to root clears `ProjectId`
- Running task drag does not break session
- Tree refresh reflects new structure

---

## Slice 7 — Context Panel

**Goal:** Full Context Panel per ADR Decisions 12–13.

**Scope:** `ContextPanelViewModel` — `ContextText` edit, deadline edit, rollup display, conversion context menus.

**Acceptance:**

- Selecting project or task-with-project shows context, deadline, rollup
- Standalone task hides panel
- Context edits persist independently of task ops
- Convert to Project/Task available from context menu

---

## Slice 8 — Deadline & Estimate Visibility

**Goal:** Surface estimates and deadlines in tree and panel.

**Scope:** Tree node spent/estimate suffixes; inline estimate edit; deadline picker in Context Panel.

**Acceptance:**

- Task shows `18h / 12h` style when estimate set
- Project shows rollup `125h / 200h`
- Deadline visible on project in Context Panel
- Tasks have no deadline field

---

## Slice 9 — Running Task Bar

**Goal:** Execution chrome per ADR Decision 7.

**Scope:** Migrate timer + Done/Waiting/Pause from `FocusViewModel` to `RunningTaskBarViewModel`.

**Acceptance:**

- Only one Running task shown
- Start task B auto-pauses A (no confirmation)
- Done/Waiting/Pause work from bar
- Timer displays session active duration

---

## Slice 10 — Quick Capture Integration

**Goal:** Always-available capture per Decision 15.

**Scope:** Capture input in Work Tree header; global hotkey; `CaptureToInbox` with `Parent=Root`.

**Acceptance:**

- Enter creates Inbox task at root
- Running task unchanged after capture
- Hotkey focuses capture from any view

---

## Slice 11 — Navigation Cleanup & Secondary Views

**Goal:** Demote obsolete primary nav.

**Scope:** Remove or secondary-link Focus, Tasks, Projects tabs; Settings + Analytics secondary; update welcome copy.

**Acceptance:**

- Primary nav: Work Tree + Settings
- No duplicate task/project management as co-primary surfaces
- `FocusView` removed or compact-only overlay

---

## Slice 12 — Polish & Validation

**Goal:** Production-ready ADR-0007 workspace.

**Scope:** Compact overlay, visual polish, dead code removal, full test pass, migration upgrade path.

**Acceptance:**

- `dotnet test` green
- Upgrade from pre-012 DB succeeds
- Manual walkthrough: capture → organize → convert → start → switch → complete → rollup correct
- No forbidden concepts in UI (milestones, snapshots, queue, momentum)

---

## Success Validation

| # | Criterion | Slices |
|---|---|---|
| 1 | Capture at root without disturbing Running task | 10 |
| 2 | Organize work in tree via drag-drop | 5, 6 |
| 3 | Convert Task↔Project per ADR rules | 3, 7 |
| 4 | Execute exactly one task at a time | 9 |
| 5 | Project context without extra navigation | 7 |
| 6 | Deadline and effort visible in workflow | 7, 8 |
| 7 | Optional estimates with project rollup | 2, 8 |
| 8 | Time tracking in Running Task Bar | 9 |

---

## Recommended First Sprint (post Slice 1)

1. Slice 2 — `EffortService`, estimate column, tree queries
2. Slice 5 (skeleton) — Work Tree shell as default with read-only tree + placeholder panels

---

## Related Artifacts

| Document | Role |
|---|---|
| [ADR-0007](./ADR-0007-worktree-workspace-n-unified-workitem-model.md) | Source of truth |
| [DOMAIN.md](./DOMAIN.md) | Product domain |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Technical architecture |
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | Full plan v3.0 |

---

*End of ROADMAP.md*
