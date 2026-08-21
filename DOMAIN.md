# DOMAIN.md

Version: V2
Product: Jetset
Status: Approved for Implementation Planning

---

# 1. Product Overview

## Vision

Jetset is a Personal Productivity Workspace for Knowledge Workers.

Jetset helps individuals manage projects, tasks, execution context, and focused work sessions while minimizing the cost of context switching between multiple parallel work streams.

---

## Problem Statement

Knowledge workers often work on multiple projects simultaneously.

When switching between tasks or projects, they frequently experience:

- Loss of context
- Forgotten progress
- Forgotten next actions
- Forgotten blockers
- Mental reload time

This context-switching cost reduces productivity.

Jetset aims to preserve work context and make it easy to resume work with minimal friction.

---

## Design Principles

### Minimal Friction

Creating or resuming work should take seconds.

Jetset is not a project management system like Jira or Asana.

---

### Task First

Task is the primary execution unit.

Most user activity revolves around tasks.

---

### Project Optional

Tasks may exist with or without a project.

Both are valid.

Examples:

- Review SSL issue
- Reply customer email
- Create DOMAIN.md

may exist without a project.

---

### Context Preservation

Work context is valuable and should be preserved.

The system should help users quickly understand:

- What was done
- What remains
- What should happen next

---

### Single User

Jetset is a personal productivity application.

There are no:

- Teams
- Organizations
- Roles
- Permissions

---

# 2. Core Domain

## Personal Work Management

Responsibilities:

- Organize work
- Manage tasks
- Manage projects
- Preserve execution context
- Track focused work
- Support productivity visibility

---

# 3. Business Capabilities

## 3.1 Task Management

### Quick Task

Create a task without assigning it to a project.

Examples:

- Review PR
- Reply Email
- Investigate Bug

---

### Project Task

Create a task associated with a project.

---

### Task Lifecycle

Manage task status throughout its lifecycle.

Proposed States:

- Active
- Blocked
- Done
- Cancelled

---

### Subtask Breakdown

Large tasks may be broken into smaller subtasks.

Example:

Task:

- Implement Student Module

Subtasks:

- Design Aggregate
- Create Repository
- Create API
- Write Tests

---

## 3.2 Project Management

### Project

A collection of related tasks.

Examples:

- School Information System
- Jetset
- MyHospital

---

### Deadline

Optional target completion date for a project.

---

### Milestone

A significant project objective.

Milestones help divide projects into manageable stages.

Example:

Project:
School Information System

Milestones:

- Domain Design
- Architecture Design
- Student Module

---

### Milestone Progress

Milestone progress is derived from completion of assigned tasks.

---

## 3.3 Context Management

### Working Context

Information required to quickly resume a task.

---

### Current Status

Current condition of the task.

---

### Last Progress

What was completed during the last work session.

---

### Next Action

The next step that should be performed.

---

### Blocker

Anything preventing progress.

---

### Notes

Additional information related to the task.

---

### Context Snapshot

A point-in-time capture of working context.

Typically created when:

- Pausing work
- Switching tasks
- Completing a session

Purpose:

Allow fast task resumption later.

---

## 3.4 Parallel Work Management

### Active Task

A task currently being worked on.

---

### Waiting Task

An active task that is temporarily paused.

---

### Task Switching

Users may switch between active tasks.

The system must preserve context during switching.

---

### Resume Queue

An ordered list of active tasks ready for continuation.

Purpose:

Help users quickly decide what to work on next.

---

## 3.5 Time Tracking

### Work Session

A focused period of work on a task.

Work sessions belong to tasks.

---

### Countdown Session

A work session with a predefined duration.

Examples:

- 25 minutes
- 45 minutes
- 60 minutes

---

### Stopwatch Session

A work session without a predefined duration.

---

### Active Duration

Total focused work time excluding pauses.

---

### Session History

Historical record of completed work sessions.

---

## 3.6 Productivity Analytics

### Focus Time

Total productive time spent working.

---

### Daily Productivity

Daily summary of work activity.

---

### Activity Heatmap

Visual representation of work activity over time.

Purpose:

Encourage consistent usage and productivity awareness.

---

### Productivity Streak

Consecutive productive days.

---

### Project Momentum

Activity and completion trend for a project.

---

### Context Switch Metrics

Statistics describing task switching behavior.

Examples:

- Number of switches
- Switching frequency

---

# 4. Domain Model

## Project

Represents a body of work.

Contains:

- Milestones
- Tasks

---

## Milestone

Represents a project objective.

Contains:

- Tasks

Belongs To:

- Project

---

## Task

Represents a unit of work.

Attributes:

- Title
- Status
- Notes
- Context

May:

- Belong to a project
- Exist independently
- Have subtasks
- Have work sessions

---

## Subtask

Represents a smaller piece of a task.

Belongs To:

- Task

---

## Context Snapshot

Represents preserved work context.

Contains:

- Current Status
- Last Progress
- Next Action
- Blocker
- Notes

Belongs To:

- Task

---

## Work Session

Represents focused execution work.

Belongs To:

- Task

Contains:

- Start Time
- End Time
- Active Duration

---

# 5. Key Business Processes

## Process 1 — Capture Work

User creates:

- Quick Task
or
- Project Task

---

## Process 2 — Plan Work

User:

- Creates Project
- Creates Milestones
- Creates Tasks
- Breaks Tasks into Subtasks

---

## Process 3 — Execute Work

User:

- Selects Task
- Starts Work Session
- Performs Work

---

## Process 4 — Pause Work

User pauses work.

System may capture Context Snapshot.

---

## Process 5 — Switch Work

User changes focus to another task.

System:

- Preserves context
- Updates active session
- Updates resume queue

---

## Process 6 — Resume Work

User selects a task from:

- Resume Queue
- Project View
- Search

System displays:

- Current Status
- Last Progress
- Next Action
- Blockers

User resumes work immediately.

---

## Process 7 — Complete Work

User marks task as Done.

Project and milestone progress are updated.

---

## Process 8 — Review Productivity

User reviews:

- Focus Time
- Daily Productivity
- Heatmap
- Streaks
- Project Momentum
- Context Switching Metrics

---

# 6. Out Of Scope (Future Versions)

The following capabilities are intentionally excluded from V2:

- Context Reload Score
- Context Freshness
- Resume Recommendation Engine
- Stale Task Detection
- WIP Health Score
- Focus Capacity Monitoring
- Productivity Coaching
- AI Productivity Assistant
- Goal Management
- Habit Management

These belong to future roadmap versions (V3+).

---

# 7. Success Criteria

Jetset V2 is successful when users can:

1. Manage projects and tasks with minimal friction.
2. Capture quick tasks without requiring project setup.
3. Preserve work context during task switching.
4. Resume work quickly after interruptions.
5. Track focused work time against tasks.
6. Visualize productivity trends over time.
7. Handle multiple parallel projects without losing momentum.