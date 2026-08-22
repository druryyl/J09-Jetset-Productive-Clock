# Jetset V2 UI — Implementation Planning

> **Status: Superseded**  
> **Superseded by:** [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) v3.0 (2026-08-22)  
> **Reason:** ADR-0007 Work Tree Workspace replaces Focus-centric UI direction. Do not use this document for implementation planning. See [ROADMAP.md](./ROADMAP.md) for current slices.

---

Planning document against the current WPF/MVVM codebase and DOMAIN.md V2. No code generated.

---

## 1. Gap Analysis

### Navigation

| Requirement | Current state | Gap |
|---|---|---|
| 4 items: Focus, Tasks, Projects, Settings | Focus, Tasks, Projects, **Analytics** | Replace Analytics tab with Settings |
| Analytics secondary only | Analytics is a primary tab | Demote to Settings link |
| Focus is default startup | `ShellArea.Focus` default ✓ | None |
| No Dashboard/Kanban/etc. | Not present ✓ | None |

**Current:** `MainWindow.xaml` has Analytics as a 4th nav button; Settings opens as a modal from Focus footer links.

---

### Focus Screen (highest priority)

| Section | Requirement | Current state | Gap |
|---|---|---|---|
| **§1 Quick Capture** | Text input; Enter → Inbox; Origin=Unplanned; does not disturb Running | TextBox + "Capture" button; `CaptureToInbox()` ✓; no Enter binding | Wire Enter in capture field; label as "Quick Capture" |
| **§2 Running Task** | Title, status, timer, project name; Done / Waiting / Stop | Shown only when session active (`IsIdle=false`); Pause/Resume/**Finish** (session end, not Done); no Waiting; idle shows clock instead | Restructure around **Running task**, not session idle; add **Done** + **Waiting**; demote timer controls |
| **§3 Ready Tasks** | Ready only; simple list; no ordering/recommendation; click to start | Ready list ✓; also **Waiting** section; `OrderPickerTasks` sorts by `LastWorkedAt`; per-row Start + "Switch & wait" | Remove Waiting from Focus; simplify to tap/click list; drop explicit ordering |
| **§4 Project Context** | Inline editable `ContextText`; hide when no project | Read-only + "Edit on project" navigates away; shows "No context yet" | Inline edit + auto-save; hide entire section when `ProjectId` is null |

**Additional Focus clutter (not in spec):**

- Idle clock/date hero when nothing is running
- "Timer options" start panel (`StartSessionViewModel`)
- Today total + streak badges (analytics on Focus)
- History / Settings / Compact footer links
- Global search in nav bar (acceptable secondary; not in wireframe)

**Domain alignment gaps:**

- `FinishCommand` ends the session via `WorkExecution.FinishWork()` → task returns to Ready; it does **not** call `TaskService.CompleteTask()` (Done)
- Running task is inferred from **session**, not `TaskStatus.Running` — usually aligned, but the UI should bind to task status as source of truth per DOMAIN.md §11.2
- Quick capture does not wire Enter (only Ctrl+Shift+C hotkey + button)

---

### Tasks Screen

| Requirement | Current state | Gap |
|---|---|---|---|
| Status groups: Inbox, Ready, Waiting, Done, Cancelled | Filter pills for Inbox/Ready/Waiting/Done + combo for all statuses; **no Cancelled pill** | Add Cancelled quick filter; consider grouped sections vs. filter-only |
| Create / edit / status / assign / detach | Master-detail with save ✓ | Minor UX: status groups could be clearer |
| No Kanban / swimlanes / drag-drop | List + detail ✓ | None |

Filter-heavy layout (project, origin, search, duplicate status combo) is more complex than the spec's "status groups" intent — simplify in a later slice.

---

### Projects Screen

| Requirement | Current state | Gap |
|---|---|---|---|
| List: Name + Task count | ✓ | None |
| Detail: Name, ContextText, related tasks | ✓ | None |
| Create / rename / delete (detach tasks) | ✓ per BR-10 | None |
| Optional deadline | Present in UI | Out of DOMAIN.md V2 core — consider removing or leaving as optional metadata (no new concept) |

---

### Settings Screen

| Requirement | Current state | Gap |
|---|---|---|
| Theme, timer prefs, hotkeys, general prefs | `SettingsWindow` modal ✓ | Promote to primary nav view |
| Analytics/history link | History from Focus footer only; no Analytics link in Settings | Add links to Analytics + History |

---

### Removed / forbidden concepts

| Concept | In codebase? | UI impact |
|---|---|---|
| Resume Queue | Service files exist; not in UI | None in UI ✓ |
| Context Snapshots | Legacy persistence; Focus shows project `ContextText` only | None in UI ✓ |
| Milestones | Legacy services; not in nav | None in UI ✓ |
| Project Momentum | Removed from analytics tests | None in UI ✓ |
| Multiple Running tasks | Enforced in `TaskService.StartTask` | None |

---

## 2. Updated Screen Inventory

| Screen | Type | Role | Primary nav? | Notes |
|---|---|---|---|---|
| **Focus** | `FocusView` | Execution hub — capture, run, pick next, project context | **Yes (default)** | Restructure to 4-section vertical stack |
| **Tasks** | `TasksView` | Full task lifecycle management | Yes | Simplify filters toward status groups |
| **Projects** | `ProjectsView` | Project list + detail + context editor | Yes | Keep master-detail |
| **Settings** | `SettingsView` (new) or promoted `SettingsWindow` | Preferences + secondary links | **Yes** | Replace Analytics tab; host Analytics/History links |
| **Analytics** | `AnalyticsView` | Personal metrics (heatmap, streak, daily focus) | **No** | Reachable from Settings |
| **History** | `HistoryWindow` | Session history by day | **No** | Reachable from Settings |
| **Compact Focus** | `FocusView` compact mode | Minimal timer overlay | No (mode) | Keep; strip non-essential chrome |
| **Recovery** | `RecoveryDialog` | Crash recovery | Modal | Keep |
| **V2 Welcome** | `V2WelcomeDialog` | Onboarding | Modal | Update copy: Settings not Analytics |

**Retire from primary shell:**

- Analytics `DataTemplate` in `MainWindow` nav (keep view, change entry point)

**Secondary / global (unchanged):**

- Global search overlay in nav bar
- Tray icon + hotkeys
- Recovery dialog

---

## 3. Navigation Map

```text
┌─────────────────────────────────────────────────────────┐
│  [Focus]  [Tasks]  [Projects]  [Settings]    [Search…]  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│              CurrentViewModel content area                │
│                                                         │
└─────────────────────────────────────────────────────────┘

Startup → Focus

Cross-navigation triggers (keep existing patterns):
  Tasks/Projects/Search "Start work"  → Focus
  Focus "Edit project context"      → Projects (selected project)  [may be removed if inline edit lands]
  Settings "View analytics"         → Analytics (embedded or dialog)
  Settings "Session history"        → HistoryWindow (modal)

Hotkeys (unchanged, from V2WelcomeViewModel):
  Ctrl+Shift+C  → Focus + quick capture focus
  Ctrl+N        → Focus + start work panel (revisit: may open Ready picker instead)
  Ctrl+P        → Pause/resume session
  Ctrl+Enter    → Finish session (revisit: may become "Mark Done" when spec actions land)
  Ctrl+M        → Compact mode
  Ctrl+H        → Show/hide window
```

**`ShellArea` enum change:**

```text
Focus, Tasks, Projects, Settings   // remove Analytics
```

---

## 4. ViewModel Changes

### 4.1 `ShellViewModel`

| Change | Detail |
|---|---|
| Replace `Analytics` nav | Add `SettingsViewModel Settings`; `NavigateSettingsCommand` |
| Remove `NavigateAnalyticsCommand` | Analytics opened from Settings event/command |
| `CurrentViewModel` switch | `ShellArea.Settings → Settings` |
| Window size hints | Settings uses planning width (720×560), same as Tasks/Projects |
| Wire `OpenAnalyticsRequested` | From Settings → show Analytics (embedded sub-view or separate window) |
| Wire `OpenHistoryRequested` | From Settings → `HistoryWindow` |

### 4.2 `FocusViewModel` (largest change)

**New / renamed properties:**

| Property | Purpose |
|---|---|
| `RunningTask` | `FocusRunningTaskViewModel?` — bound when `TaskService.GetRunningTask()` is non-null |
| `HasRunningTask` | `RunningTask is not null` |
| `RunningTaskTitle`, `RunningTaskStatus`, `RunningProjectName` | Denormalized for XAML |
| `EditableProjectContext` | Two-way bound `ContextText` for running task's project |
| `HasProjectContextSection` | `RunningTask?.ProjectId is not null` |
| `ReadyTasks` | Keep; remove ordering by `LastWorkedAt` → store order or `CreatedAt` |
| `QuickCaptureTitle` | Keep |

**Remove / relocate:**

| Item | Action |
|---|---|
| `WaitingTasks` / `HasWaitingTasks` | Remove from Focus VM |
| `IsIdle` clock hero | Remove or collapse to minimal empty state |
| `TodayTotalText`, `StreakText`, `HasStreak` | Move to Analytics or remove from Focus |
| `ShowStartPanel`, `StartSession` timer panel | Defer to Settings default timer OR slim "advanced" link; starting from Ready list should use default stopwatch |
| `EditProjectContextCommand` + event | Replace with `SaveProjectContextCommand` inline |
| `OpenHistoryCommand`, `OpenSettingsCommand` | Remove (nav handles Settings; History in Settings) |

**New commands:**

| Command | Behavior |
|---|---|
| `MarkDoneCommand` | `CompleteTask(runningId)` + finish session if active |
| `MoveToWaitingCommand` | `StopTask(id, Waiting)` or `ChangeStatus(Waiting)` + end session |
| `StopCommand` | `StopTask(id, Ready)` + end session (optional; spec says "if supported") |
| `StartReadyTaskCommand` | `WorkExecution.StartWork(taskId)` — previous Running → Ready |
| `SaveProjectContextCommand` | `Projects.UpdateContextText(projectId, text)` on blur or debounced auto-save |

**Refactor `RefreshFromSession()`:**

- Derive Running section from `_services.Tasks.GetRunningTask()` first, session second (timer display)
- When no Running task: show empty Running section or prompt ("No task running — pick one below"), not a large clock

**Quick capture:**

- Add `QuickCaptureOnEnter` behavior (View code-behind `KeyDown` or `InputBinding`)
- Confirm `CaptureToInbox` defaults Origin=Unplanned ✓

### 4.3 `TasksViewModel`

| Change | Detail |
|---|---|
| Add Cancelled quick-filter pill | Mirror Inbox/Ready/Waiting/Done buttons |
| Optional: `StatusSections` collection | Grouped `InboxTasks`, `ReadyTasks`, etc. for accordion UI (later slice) |
| Keep master-detail | No structural VM change required for MVP |

### 4.4 `ProjectsViewModel`

| Change | Detail |
|---|---|
| Minimal | Already matches spec |
| Optional cleanup | Remove deadline UI if simplifying (not required for V2 core) |

### 4.5 `SettingsViewModel` (extend)

| Change | Detail |
|---|---|
| Promote to shell-hosted VM | Same properties as today |
| Add `OpenAnalyticsCommand` | Raises event → Shell shows Analytics |
| Add `OpenHistoryCommand` | Raises event → `HistoryWindow` |
| Add `DefaultTimerMode` / `DefaultCountdownMinutes` | If timer panel removed from Focus, configure defaults here |

### 4.6 `AnalyticsViewModel`

| Change | Detail |
|---|---|
| None required | Keep as-is; only entry point changes |

### 4.7 New small VMs (optional)

| VM | Purpose |
|---|---|
| `FocusRunningTaskViewModel` | Id, Title, Status, ProjectName, ProjectId, TimerDisplay, IsPaused |
| `SettingsShellViewModel` | Wrapper if Settings view embeds Analytics sub-panel |

Prefer extending existing VMs over new layers unless Focus VM exceeds ~400 lines after refactor.

---

## 5. XAML Layout Proposal

### 5.1 `MainWindow.xaml`

```text
┌──────────────────────────────────────────────────┐
│ [Focus] [Tasks] [Projects] [Settings]  [Search]│
├──────────────────────────────────────────────────┤
│ <ContentControl Content="{Binding CurrentVM}" /> │
└──────────────────────────────────────────────────┘
```

- Remove Analytics button and `AnalyticsViewModel` DataTemplate from primary content
- Add Settings button + `SettingsView` DataTemplate
- Keep search overlay unchanged

### 5.2 `FocusView.xaml` — expanded mode (target)

```text
┌─────────────────────────────────────┐
│ Quick Capture                       │
│ ┌─────────────────────────────────┐ │
│ │ [ text input              ]     │ │  ← Enter creates Inbox task
│ └─────────────────────────────────┘ │
├─────────────────────────────────────┤
│ RUNNING TASK                        │
│                                     │
│ Implement JWT Middleware            │  ← Title (SemiBold 18)
│ Running · DDD Lite                  │  ← Status + Project (secondary)
│ 01:24:32                            │  ← Timer (monospace 36)
│                                     │
│ [Done]  [Waiting]  [Pause] [Stop?]  │
│                                     │
│ (empty state when no running task:  │
│  "No task running" — no clock hero) │
├─────────────────────────────────────┤
│ Ready Tasks                         │
│ • Review Security                   │  ← ListBox, click = Start
│ • SSL Investigation                 │
│ • Update Documentation              │
├─────────────────────────────────────┤
│ Project Context          [hidden]   │  ← Visibility=HasProjectContextSection
│ ┌─────────────────────────────────┐ │
│ │ DDD Lite                        │ │  ← project name label
│ │ SQLite                          │ │
│ │ Auth Review Pending             │ │  ← editable TextBox, auto-save
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘
│ [Compact]                           │  ← footer: compact toggle only
```

**Binding sketch:**

```xml
<!-- Section 1 -->
<TextBlock Text="Quick Capture" Style="{StaticResource SectionHeader}" />
<TextBox Text="{Binding QuickCaptureTitle, UpdateSourceTrigger=PropertyChanged}"
         InputBindings → QuickCaptureCommand on Enter />

<!-- Section 2 -->
<Border Visibility="{Binding HasRunningTask, Converter=BoolToVis}">
  <StackPanel>
    <TextBlock Text="RUNNING TASK" Style="{StaticResource SectionLabel}" />
    <TextBlock Text="{Binding RunningTask.Title}" />
    <TextBlock Text="{Binding RunningTask.StatusAndProject}" />
    <TextBlock Text="{Binding RunningTask.TimerDisplay}" FontFamily="Monospace" FontSize="36" />
    <StackPanel Orientation="Horizontal">
      <Button Content="Done" Command="{Binding MarkDoneCommand}" />
      <Button Content="Waiting" Command="{Binding MoveToWaitingCommand}" />
      <!-- Pause/Resume/Stop as secondary timer controls -->
    </StackPanel>
  </StackPanel>
</Border>

<!-- Section 3 -->
<TextBlock Text="Ready Tasks" />
<ListBox ItemsSource="{Binding ReadyTasks}"
         SelectionChanged / MouseDoubleClick → StartReadyTaskCommand />

<!-- Section 4 -->
<StackPanel Visibility="{Binding HasProjectContextSection, Converter=BoolToVis}">
  <TextBlock Text="Project Context" />
  <TextBlock Text="{Binding RunningProjectName}" />
  <TextBox Text="{Binding EditableProjectContext, UpdateSourceTrigger=PropertyChanged}"
           AcceptsReturn="True" TextWrapping="Wrap" MinHeight="80" />
</StackPanel>
```

**Compact mode:** Keep timer + capture + Done/Waiting; hide Ready list and Project Context (or collapse to single line).

### 5.3 `TasksView.xaml` — incremental

- Add **Cancelled** to quick-filter pill row
- Later slice: replace dual status filter (pills + combo) with single source of truth

### 5.4 `ProjectsView.xaml`

- No structural change required
- Optional: remove deadline panel

### 5.5 `SettingsView.xaml` (new UserControl)

```text
Settings
├── Appearance: Dark theme, Always on top
├── Timer: Default mode, countdown duration, idle auto-pause
├── Hotkeys: shortcut list (read-only)
├── General: 24h clock, seconds, start with Windows, sound
├── Links: [View Analytics] [Session History]
└── [Save]
```

Promote content from `SettingsWindow.xaml` into `SettingsView.xaml`; keep `SettingsWindow` as thin host or remove in favor of in-shell view.

---

## 6. Slice-Based Implementation Plan

Each slice is independently reviewable. Order respects dependencies.

---

### Slice 0 — Planning baseline (this document)

**Deliverable:** Approved plan  
**Risk:** None  
**Tests:** None

---

### Slice 1 — Navigation realignment

**Scope:**

- `ShellArea`: add `Settings`, remove `Analytics`
- `MainWindow.xaml`: swap Analytics tab → Settings tab
- Create `SettingsView` + host existing `SettingsViewModel` content
- Move History/Analytics entry points to Settings links
- Update `V2WelcomeDialog` copy (Analytics → Settings)

**Files:** `ShellArea.cs`, `ShellViewModel.cs`, `MainWindow.xaml`, new `SettingsView.xaml`, `SettingsWindow.xaml` (deprecate or delegate)

**Acceptance:**

- App starts on Focus
- 4 nav tabs: Focus, Tasks, Projects, Settings
- Analytics reachable only from Settings
- History reachable from Settings
- Existing settings persist correctly

**Tests:** Update any `ShellViewModel` navigation tests

---

### Slice 2 — Focus layout skeleton (XAML only, minimal VM)

**Scope:**

- Restructure `FocusView.xaml` expanded mode into 4 labeled sections
- Remove Waiting section, streak, today total, History/Settings footer links from Focus
- Empty Running state placeholder (no clock hero)
- Keep existing bindings where possible (temporary)

**Acceptance:**

- Visual structure matches wireframe
- No functional regressions (existing commands still reachable)

**Tests:** None required (layout only)

---

### Slice 3 — Running Task actions (Done / Waiting / Stop)

**Scope:**

- `FocusViewModel`: `MarkDoneCommand`, `MoveToWaitingCommand`, optional `StopCommand`
- Wire to `TaskService.CompleteTask`, `StopTask`, `WorkExecution.FinishWork`
- Running section binds to `GetRunningTask()` + session timer
- Remove `Finish` as primary action (or map Ctrl+Enter → Mark Done — decide in review)

**Acceptance:**

- Done marks task Done, releases Running slot, ends session
- Waiting moves Running task to Waiting, ends session
- Only one Running task globally (existing invariant holds)
- Starting Ready task switches per §4.2 default (previous → Ready)

**Tests:** `FocusViewModelTests` for Done/Waiting; extend `WorkExecutionServiceTests`

---

### Slice 4 — Quick Capture polish

**Scope:**

- Enter key in capture TextBox triggers `QuickCaptureCommand`
- Section header "Quick Capture"
- Remove separate "Capture to Inbox" button (Enter-only UX) OR keep button as secondary
- Ensure capture never changes Running task (already true)

**Acceptance:**

- Type title + Enter → Inbox task, field clears, Running unchanged
- Ctrl+Shift+C still focuses capture field

**Tests:** Focus quick capture Enter behavior

---

### Slice 5 — Ready Tasks simplification

**Scope:**

- Remove `WaitingTasks` from Focus VM + XAML
- Remove `OrderPickerTasks` LastWorkedAt sort → insertion/store order
- Simplify item template: title only (optional project subtitle); single click/tap starts task
- Remove per-row "Switch & wait" from Ready list (Waiting is a Running-task action)

**Acceptance:**

- Ready list matches spec: simple bullets, user picks explicitly
- Start Ready task → becomes Running; previous Running → Ready

**Tests:** Update `FocusViewModelTests` for picker behavior

---

### Slice 6 — Inline Project Context on Focus

**Scope:**

- Replace read-only context + "Edit on project" with editable `TextBox`
- `EditableProjectContext` two-way bind; save on `LostFocus` or 500ms debounce
- `HasProjectContextSection` false when Running task has no `ProjectId` — no placeholder text
- Remove `EditProjectContextRequested` event from Focus → Shell → Projects path

**Acceptance:**

- Context edits persist via `ProjectService.UpdateContextText`
- Section hidden for standalone Running tasks
- No task-level context fields introduced

**Tests:** Focus context save; visibility when no project

---

### Slice 7 — Timer UX decoupling

**Scope:**

- Move "Timer options" panel off Focus (or behind "Timer settings" link)
- Starting a Ready task uses Settings default (stopwatch)
- Keep Pause/Resume on Running section as secondary session controls
- Settings: add default timer mode preference

**Acceptance:**

- Focus no longer dominated by timer configuration
- Session still starts when task starts (DOMAIN.md §7.1)

**Tests:** Settings default applied on `StartWork`

---

### Slice 8 — Tasks screen status groups

**Scope:**

- Add Cancelled quick-filter button
- Remove redundant status ComboBox (or sync with pills)
- Optional: visual section headers when filter = All

**Acceptance:**

- All 5 status groups accessible
- Create/edit/assign/detach unchanged

**Tests:** `TasksViewModelTests` for Cancelled filter

---

### Slice 9 — Compact mode + polish

**Scope:**

- Align compact `FocusView` with new Running actions (Done/Waiting)
- Footer: Compact toggle only
- Window default size: Focus 360×480 ✓
- Visual pass: section dividers, typography per wireframe

**Acceptance:**

- Compact mode usable as floating execution widget
- Expanded mode matches approved layout

---

### Slice 10 — Cleanup & docs

**Scope:**

- Remove dead Focus properties (`TodayTotalText`, `StreakText`, etc.)
- Update `V2WelcomeViewModel.Highlights`
- Confirm no forbidden UI concepts reintroduced
- Optional: remove project deadline UI

**Acceptance:**

- `dotnet test` green
- Manual walkthrough of DOMAIN.md success criteria §10

---

## Summary

The codebase is **~70% aligned** with the V2 Focus-centric spec. Core domain services (`TaskService`, `WorkExecutionService`, `CaptureToInbox`) already support the right behaviors. The main work is **UI restructuring and navigation**:

1. **Demote Analytics**, **promote Settings** to primary nav
2. **Rebuild Focus** as a strict 4-section stack
3. Add **Done / Waiting** as first-class Running-task actions
4. Make **Project Context inline-editable** and conditionally visible
5. **Simplify Ready list** and remove Focus clutter (Waiting section, analytics badges, timer panel)

Slices 1–3 deliver the highest user-visible value; slices 4–6 complete DOMAIN.md Focus requirements; slices 7–10 are polish and consistency.
