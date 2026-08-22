# ADR-0007 — Work Tree Workspace & Unified Work Item Model

## Status

Accepted

## Date

2026-08-22

## Decision Makers

Product Owner

---

# Context

Jetset V2 originally evolved around a "Focus Workspace" concept where the primary screen emphasized the currently running task.

Further workflow analysis revealed that the actual user workflow is different.

The user's real-world workflow is:

```text
Capture
↓
Organize
↓
Work
↓
Pause / Switch
↓
Resume
↓
Complete
```

The central object in the workflow is not the currently running task.

The central object is:

```text
Work Tree
```

The user continuously:

- captures new work items
- reorganizes work items
- groups tasks into projects
- moves tasks between projects
- starts and pauses work
- tracks actual effort
- maintains project context

Therefore the primary workspace must optimize for work organization and execution simultaneously.

---

# Decision

Jetset V2 shall adopt a:

> Work Tree Workspace

instead of a Focus Workspace.

The Work Tree becomes the primary screen and primary navigation destination.

---

# Decision 1 — Unified Work Item Model

Jetset shall use a unified work item model.

```text
WorkItem
├── Task
└── Project
```

Projects and Tasks are two forms of the same conceptual object.

---

# Decision 2 — Task to Project Conversion

Users may convert a Task into a Project.

Example:

```text
Implement Jetset V2
```

becomes:

```text
Jetset V2
├── Authentication
├── UI Design
└── Domain Review
```

The original task becomes a project container.

---

# Decision 3 — Project to Task Conversion

Users may convert a Project back into a Task.

Constraint:

```text
Project.Children.Count == 0
```

Projects containing children cannot be converted.

Children must be removed first.

---

# Decision 4 — Hierarchical Work Tree

The application shall present work as a tree structure.

Example:

```text
Jetset V2
├── Authentication
├── UI Design
└── Domain Review

SIS
├── Student Lifecycle
└── Academic Delivery

SSL Investigation
```

The work tree is the primary interaction surface.

---

# Decision 5 — Project Membership by Drag & Drop

Users may:

```text
Task → Project
```

by drag-and-drop.

Example:

```text
Authentication
```

dropped into:

```text
Jetset V2
```

becomes:

```text
Jetset V2
└── Authentication
```

Users may also move items out of projects.

---

# Decision 6 — Expand / Collapse Projects

Projects support:

```text
Expanded
Collapsed
```

states.

Example:

```text
▼ Jetset V2
  Authentication
  UI Design
```

or

```text
▶ Jetset V2
```

This is a UI concern only.

Expansion state is not part of domain behavior.

---

# Decision 7 — Single Running Task

Only one Task may be running at any time.

Starting another task automatically pauses the previous task.

Example:

```text
Task A = Running

Start Task B

Task A = Paused
Task B = Running
```

No confirmation is required.

This behavior is intentional.

---

# Decision 8 — Time Tracking

Every task accumulates actual effort.

Example:

```text
Authentication

Spent:
18h 20m
```

Actual effort is derived from recorded work sessions.

---

# Decision 9 — Optional Task Estimate

Tasks may optionally contain an estimate.

Example:

```text
Authentication

Estimate:
12h

Spent:
18h
```

Estimate is optional.

Users are not required to estimate work.

---

# Decision 10 — Project Effort Rollup

Projects display aggregated effort.

Project effort shall be calculated.

Example:

```text
Jetset V2

Spent:
125h

Estimate:
200h
```

Calculation:

```text
ProjectSpent
=
Sum(ChildTaskSpent)
```

and

```text
ProjectEstimate
=
Sum(ChildTaskEstimate)
```

No manual project effort entry exists in V2.

Project effort is derived.

---

# Decision 11 — Project Deadline

Deadline belongs exclusively to Projects.

Tasks do not have deadlines.

Example:

```text
Jetset V2

Deadline:
31 Dec 2026
```

Deadline is visible directly within normal workflow.

Users should not need to open a separate screen to see deadlines.

---

# Decision 12 — Project Context Ownership

Projects own context.

Tasks do not.

Example:

```text
Jetset V2

Context:
- Current architecture
- Important notes
- Next objectives
```

Project context is editable.

Project context is persistent.

---

# Decision 13 — Context Visibility

Project context must be easily accessible.

Jetset shall provide a Context Panel associated with the selected project or the project owning the selected task.

The user must not need multiple navigation steps to access context.

---

# Decision 14 — Primary Workspace Layout

The primary application layout becomes:

```text
┌──────────────────────┬─────────────────────┐
│                      │                     │
│ Work Tree            │ Context Panel       │
│                      │                     │
└──────────────────────┴─────────────────────┘

Running Task Bar
```

Where:

Left:

```text
Work Tree
```

Right:

```text
Project Context
Deadline
Estimate
Spent
```

Bottom:

```text
Current Running Task
Timer
Controls
```

---

# Decision 15 — Quick Capture

Users can create tasks instantly.

Examples:

```text
Fix SSL

Review Architecture

Create UI Mockup
```

Default behavior:

```text
Type = Task
Parent = Root
```

Quick Capture remains available at all times.

---

# Explicit Non-Goals

Jetset V2 shall not introduce:

- Sprint Management
- Milestones
- Story Points
- Goal Tracking
- Kanban Boards
- WIP Limits
- Priority Engines
- Recommendation Engines
- Productivity Scores
- AI Coaching
- Context Freshness
- Context Snapshots
- Project Momentum

These concepts are intentionally outside V2 scope.

---

# Consequences

## Positive

- Matches actual user workflow.
- Reduces navigation complexity.
- Makes projects emerge naturally from tasks.
- Simplifies project organization.
- Keeps time tracking highly visible.
- Keeps project context accessible.

## Negative

- Tree interaction requires drag-drop implementation.
- Project rollup calculations must be implemented.
- Existing Focus Workspace artifacts may require revision.

---

# Required Follow-Up

The following artifacts must be reviewed for alignment:

- DOMAIN.md
- ARCHITECTURE.md
- ROADMAP.md
- Existing UI specifications
- Existing implementation plans

Any conflicting Focus Workspace assumptions must be updated before implementation planning proceeds.