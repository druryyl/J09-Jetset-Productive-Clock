using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace Jetset.App.ViewModels;

public enum ProjectFilterMode
{
    All,
    Unassigned,
    Project
}

public enum StatusFilterMode
{
    All,
    ActiveWork,
    Active,
    Blocked,
    Done,
    Cancelled
}

public sealed class ProjectFilterOption
{
    public ProjectFilterMode Mode { get; init; }

    public Guid? ProjectId { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public sealed class StatusFilterOption
{
    public StatusFilterMode Mode { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public sealed class ProjectAssignOption
{
    public Guid? ProjectId { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public sealed class MilestoneAssignOption
{
    public Guid? MilestoneId { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public sealed class TasksViewModel : ObservableObject
{
    private readonly AppServices _services;
    private TaskListItemViewModel? _selected;
    private string _searchText = string.Empty;
    private string _quickAddTitle = string.Empty;
    private ProjectFilterOption? _selectedFilter;
    private StatusFilterOption? _selectedStatusFilter;
    private ProjectAssignOption? _selectedProjectOption;
    private MilestoneAssignOption? _selectedMilestoneOption;
    private TaskStatus _loadedStatus;
    private string? _message;
    private string _latestSnapshotSummary = string.Empty;
    private string _focusTimeText = string.Empty;
    private bool _suppressFilterReload;
    private bool _suppressMilestoneRebuild;

    public TasksViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<TaskListItemViewModel>();
        Snapshots = new ObservableCollection<ContextSnapshotItemViewModel>();
        FilterOptions = new ObservableCollection<ProjectFilterOption>();
        StatusFilterOptions = new ObservableCollection<StatusFilterOption>
        {
            new() { Mode = StatusFilterMode.All, DisplayName = "All statuses" },
            new() { Mode = StatusFilterMode.ActiveWork, DisplayName = "Active work" },
            new() { Mode = StatusFilterMode.Active, DisplayName = "Active" },
            new() { Mode = StatusFilterMode.Blocked, DisplayName = "Blocked" },
            new() { Mode = StatusFilterMode.Done, DisplayName = "Done" },
            new() { Mode = StatusFilterMode.Cancelled, DisplayName = "Cancelled" }
        };
        ProjectOptions = new ObservableCollection<ProjectAssignOption>();
        MilestoneOptions = new ObservableCollection<MilestoneAssignOption>();
        StatusOptions = Enum.GetValues<TaskStatus>();

        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        ReopenCommand = new RelayCommand(Reopen, () => Selected?.CanReopen == true);
        CaptureSnapshotCommand = new RelayCommand(CaptureSnapshot, () => Selected is not null);
        StartWorkCommand = new RelayCommand(BeginWork, CanExecuteStartWork);
        StartWorkForTaskCommand = new RelayCommand(BeginWorkForTask);
        ResumeWorkCommand = new RelayCommand(ResumeWork, CanExecuteResumeWork);
        ResumeWorkForTaskCommand = new RelayCommand(ResumeWorkForTask);
        RefreshCommand = new RelayCommand(Load);

        RebuildProjectOptions();
        _selectedFilter = FilterOptions[0];
        _selectedStatusFilter = StatusFilterOptions[0];
        Load();
    }

    public ObservableCollection<TaskListItemViewModel> Items { get; }

    public ObservableCollection<ContextSnapshotItemViewModel> Snapshots { get; }

    public ObservableCollection<ProjectFilterOption> FilterOptions { get; }

    public ObservableCollection<StatusFilterOption> StatusFilterOptions { get; }

    public ObservableCollection<ProjectAssignOption> ProjectOptions { get; }

    public ObservableCollection<MilestoneAssignOption> MilestoneOptions { get; }

    public TaskStatus[] StatusOptions { get; }

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ReopenCommand { get; }
    public RelayCommand CaptureSnapshotCommand { get; }
    public RelayCommand StartWorkCommand { get; }
    public RelayCommand StartWorkForTaskCommand { get; }
    public RelayCommand ResumeWorkCommand { get; }
    public RelayCommand ResumeWorkForTaskCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public event EventHandler? WorkStarted;

    public event EventHandler<ContextCaptureRequest>? ContextCaptureRequested;

    public TaskListItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                _loadedStatus = value?.Status ?? TaskStatus.Active;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanReopenSelected));
                OnPropertyChanged(nameof(CanStartWorkSelected));
                OnPropertyChanged(nameof(CanResumeWorkSelected));
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                ReopenCommand.RaiseCanExecuteChanged();
                CaptureSnapshotCommand.RaiseCanExecuteChanged();
                StartWorkCommand.RaiseCanExecuteChanged();
                ResumeWorkCommand.RaiseCanExecuteChanged();
                SyncSelectedProjectOption();
                RebuildMilestoneOptions(SelectedProjectOption?.ProjectId);
                SyncSelectedMilestoneOption();
                OnPropertyChanged(nameof(CanAssignMilestone));
                LoadSnapshots();
                LoadFocusTime();
            }
        }
    }

    public string FocusTimeText
    {
        get => _focusTimeText;
        private set => SetProperty(ref _focusTimeText, value);
    }

    public bool HasFocusTime => !string.IsNullOrWhiteSpace(FocusTimeText);

    public bool HasSelection => Selected is not null;

    public bool HasItems => Items.Count > 0;

    public bool HasSnapshots => Snapshots.Count > 0;

    public bool HasLatestSnapshot => !string.IsNullOrWhiteSpace(LatestSnapshotSummary);

    public string LatestSnapshotSummary
    {
        get => _latestSnapshotSummary;
        private set
        {
            if (SetProperty(ref _latestSnapshotSummary, value))
            {
                OnPropertyChanged(nameof(HasLatestSnapshot));
            }
        }
    }

    public bool CanAssignMilestone => SelectedProjectOption?.ProjectId is not null;

    public bool CanReopenSelected => Selected?.CanReopen == true;

    public bool CanResumeWorkSelected => Selected?.CanResumeWork == true;

    public bool CanStartWorkSelected => Selected?.CanStartWork == true;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Load();
            }
        }
    }

    public ProjectFilterOption? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value) && !_suppressFilterReload)
            {
                Load();
            }
        }
    }

    public StatusFilterOption? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value) && !_suppressFilterReload)
            {
                Load();
            }
        }
    }

    public ProjectAssignOption? SelectedProjectOption
    {
        get => _selectedProjectOption;
        set
        {
            if (SetProperty(ref _selectedProjectOption, value))
            {
                if (!_suppressMilestoneRebuild)
                {
                    RebuildMilestoneOptions(value?.ProjectId);
                    if (SelectedMilestoneOption?.MilestoneId is not null &&
                        MilestoneOptions.All(m => m.MilestoneId != SelectedMilestoneOption.MilestoneId))
                    {
                        SelectedMilestoneOption = MilestoneOptions[0];
                    }

                    OnPropertyChanged(nameof(CanAssignMilestone));
                }
            }
        }
    }

    public MilestoneAssignOption? SelectedMilestoneOption
    {
        get => _selectedMilestoneOption;
        set => SetProperty(ref _selectedMilestoneOption, value);
    }

    public string QuickAddTitle
    {
        get => _quickAddTitle;
        set
        {
            if (SetProperty(ref _quickAddTitle, value))
            {
                AddTaskCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    private bool CanAddTask() => !string.IsNullOrWhiteSpace(QuickAddTitle);

    private void RebuildProjectOptions()
    {
        var previousMode = SelectedFilter?.Mode;
        var previousFilterId = SelectedFilter?.ProjectId;

        FilterOptions.Clear();
        FilterOptions.Add(new ProjectFilterOption
        {
            Mode = ProjectFilterMode.All,
            DisplayName = "All tasks",
            ProjectId = null
        });
        FilterOptions.Add(new ProjectFilterOption
        {
            Mode = ProjectFilterMode.Unassigned,
            DisplayName = "No project",
            ProjectId = null
        });

        ProjectOptions.Clear();
        ProjectOptions.Add(new ProjectAssignOption { DisplayName = "None", ProjectId = null });

        foreach (var project in _services.Projects.ListProjects())
        {
            FilterOptions.Add(new ProjectFilterOption
            {
                Mode = ProjectFilterMode.Project,
                DisplayName = project.Name,
                ProjectId = project.Id
            });
            ProjectOptions.Add(new ProjectAssignOption
            {
                DisplayName = project.Name,
                ProjectId = project.Id
            });
        }

        _suppressFilterReload = true;
        try
        {
            SelectedFilter = previousMode switch
            {
                ProjectFilterMode.Unassigned => FilterOptions[1],
                ProjectFilterMode.Project when previousFilterId is { } id =>
                    FilterOptions.FirstOrDefault(f => f.ProjectId == id) ?? FilterOptions[0],
                _ => FilterOptions[0]
            };
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private void RebuildMilestoneOptions(Guid? projectId)
    {
        var previousMilestoneId = SelectedMilestoneOption?.MilestoneId;

        MilestoneOptions.Clear();
        MilestoneOptions.Add(new MilestoneAssignOption { DisplayName = "None", MilestoneId = null });

        if (projectId is { } pid)
        {
            foreach (var milestone in _services.Milestones.ListByProject(pid))
            {
                MilestoneOptions.Add(new MilestoneAssignOption
                {
                    DisplayName = milestone.Name,
                    MilestoneId = milestone.Id
                });
            }
        }

        _suppressMilestoneRebuild = true;
        try
        {
            SelectedMilestoneOption = previousMilestoneId is { } mid
                ? MilestoneOptions.FirstOrDefault(m => m.MilestoneId == mid) ?? MilestoneOptions[0]
                : MilestoneOptions[0];
        }
        finally
        {
            _suppressMilestoneRebuild = false;
        }
    }

    private void SyncSelectedProjectOption()
    {
        if (Selected is null)
        {
            SelectedProjectOption = null;
            return;
        }

        _suppressMilestoneRebuild = true;
        try
        {
            SelectedProjectOption = ProjectOptions.FirstOrDefault(p => p.ProjectId == Selected.ProjectId)
                ?? ProjectOptions[0];
        }
        finally
        {
            _suppressMilestoneRebuild = false;
        }
    }

    private void SyncSelectedMilestoneOption()
    {
        if (Selected is null)
        {
            SelectedMilestoneOption = null;
            return;
        }

        SelectedMilestoneOption = MilestoneOptions.FirstOrDefault(m => m.MilestoneId == Selected.MilestoneId)
            ?? MilestoneOptions[0];
    }

    private Dictionary<Guid, string> BuildProjectNameMap() =>
        _services.Projects.ListProjects().ToDictionary(p => p.Id, p => p.Name);

    private Dictionary<Guid, string> BuildMilestoneNameMap()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var project in _services.Projects.ListProjects())
        {
            foreach (var milestone in _services.Milestones.ListByProject(project.Id))
            {
                map[milestone.Id] = milestone.Name;
            }
        }

        return map;
    }

    private static bool MatchesStatusFilter(WorkTask task, StatusFilterMode mode) =>
        mode switch
        {
            StatusFilterMode.All => true,
            StatusFilterMode.ActiveWork => TaskStatusRules.IsEligibleForActiveWork(task.Status),
            StatusFilterMode.Active => task.Status == TaskStatus.Active,
            StatusFilterMode.Blocked => task.Status == TaskStatus.Blocked,
            StatusFilterMode.Done => task.Status == TaskStatus.Done,
            StatusFilterMode.Cancelled => task.Status == TaskStatus.Cancelled,
            _ => true
        };

    private void Load()
    {
        RebuildProjectOptions();

        var selectedId = Selected?.Id;
        Items.Clear();

        var projectNames = BuildProjectNameMap();
        var milestoneNames = BuildMilestoneNameMap();
        IEnumerable<WorkTask> tasks;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            tasks = _services.Tasks.Search(SearchText);
            tasks = SelectedFilter?.Mode switch
            {
                ProjectFilterMode.Project when SelectedFilter.ProjectId is { } projectId =>
                    tasks.Where(t => t.ProjectId == projectId),
                ProjectFilterMode.Unassigned => tasks.Where(t => t.ProjectId is null),
                _ => tasks
            };
        }
        else
        {
            tasks = SelectedFilter?.Mode switch
            {
                ProjectFilterMode.Project when SelectedFilter.ProjectId is { } projectId =>
                    _services.Tasks.ListByProject(projectId),
                ProjectFilterMode.Unassigned => _services.Tasks.ListByProject(null),
                _ => _services.Tasks.List()
            };
        }

        var statusMode = SelectedStatusFilter?.Mode ?? StatusFilterMode.All;
        tasks = tasks.Where(t => MatchesStatusFilter(t, statusMode));

        foreach (var task in tasks)
        {
            string? projectName = null;
            if (task.ProjectId is { } pid)
            {
                projectNames.TryGetValue(pid, out projectName);
            }

            string? milestoneName = null;
            if (task.MilestoneId is { } mid)
            {
                milestoneNames.TryGetValue(mid, out milestoneName);
            }

            Items.Add(new TaskListItemViewModel(task, projectName, milestoneName));
        }

        ApplyWorkSessionState();

        Selected = selectedId is { } id
            ? Items.FirstOrDefault(i => i.Id == id)
            : null;

        OnPropertyChanged(nameof(HasItems));
        StartWorkCommand.RaiseCanExecuteChanged();
        ResumeWorkCommand.RaiseCanExecuteChanged();
    }

    private void ApplyWorkSessionState()
    {
        var execution = _services.WorkExecution;
        foreach (var item in Items)
        {
            item.HasPausedSession = execution.HasPausedSession(item.Id);
            item.IsActiveSession = execution.IsTaskFocused(item.Id);
        }
    }

    private bool CanExecuteStartWork() => Selected?.CanStartWork == true;

    private void BeginWork()
    {
        if (Selected is null)
        {
            return;
        }

        BeginWorkForTask(Selected.Id);
    }

    private void BeginWorkForTask(object? parameter)
    {
        if (!TryParseTaskId(parameter, out var taskId))
        {
            return;
        }

        Message = null;
        try
        {
            if (!TryPromptLeavingContext(out var leavingContext))
            {
                return;
            }

            _services.WorkExecution.StartWork(taskId, leavingContext: leavingContext);
            Load();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work started.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private bool CanExecuteResumeWork() => Selected?.CanResumeWork == true;

    private void ResumeWork()
    {
        if (Selected is null)
        {
            return;
        }

        ResumeWorkForTask(Selected.Id);
    }

    private void ResumeWorkForTask(object? parameter)
    {
        if (!TryParseTaskId(parameter, out var taskId))
        {
            return;
        }

        Message = null;
        try
        {
            if (!TryPromptLeavingContext(out var leavingContext))
            {
                return;
            }

            _services.WorkExecution.ResumeWork(taskId, leavingContext);
            Load();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work resumed.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private bool TryPromptLeavingContext(out WorkingContext? leavingContext)
    {
        leavingContext = null;
        var task = _services.WorkExecution.GetLeavingTask();
        if (task is null)
        {
            return true;
        }

        var request = new ContextCaptureRequest
        {
            Task = task,
            Reason = ContextCaptureReason.Switch
        };
        ContextCaptureRequested?.Invoke(this, request);

        if (request.Result == ContextCaptureResult.Cancelled)
        {
            return false;
        }

        if (request.Result == ContextCaptureResult.Saved)
        {
            leavingContext = request.Context;
        }

        return true;
    }

    private static bool TryParseTaskId(object? parameter, out Guid taskId)
    {
        switch (parameter)
        {
            case Guid id:
                taskId = id;
                return true;
            case TaskListItemViewModel item:
                taskId = item.Id;
                return true;
            case string text when Guid.TryParse(text, out var parsed):
                taskId = parsed;
                return true;
            default:
                taskId = Guid.Empty;
                return false;
        }
    }

    private void AddTask()
    {
        Message = null;
        try
        {
            Guid? projectId = SelectedFilter?.Mode == ProjectFilterMode.Project
                ? SelectedFilter.ProjectId
                : null;

            var created = _services.Tasks.Create(QuickAddTitle, projectId);
            QuickAddTitle = string.Empty;
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == created.Id);
            Message = "Task created.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void Save()
    {
        if (TrySaveSelected(out _, out var error))
        {
            Message = "Task updated.";
        }
        else if (error is not null)
        {
            Message = error;
        }
    }

    private void CaptureSnapshot()
    {
        if (Selected is null)
        {
            return;
        }

        Message = null;
        if (!TrySaveSelected(out var taskId, out var saveError))
        {
            Message = saveError ?? "Could not save task before capturing snapshot.";
            return;
        }

        try
        {
            _services.ContextSnapshots.Capture(taskId);
            LoadSnapshots();
            Message = "Snapshot captured.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private bool TrySaveSelected(out Guid taskId, out string? error)
    {
        taskId = Guid.Empty;
        error = null;

        if (Selected is null)
        {
            return false;
        }

        try
        {
            var projectId = SelectedProjectOption?.ProjectId;
            var milestoneId = projectId is null ? null : SelectedMilestoneOption?.MilestoneId;
            var newStatus = Selected.Status;

            var updated = new WorkTask
            {
                Id = Selected.Id,
                Title = Selected.Title,
                Status = Selected.Status,
                ProjectId = projectId,
                MilestoneId = milestoneId,
                CreatedAt = Selected.Task.CreatedAt,
                UpdatedAt = Selected.Task.UpdatedAt,
                LastWorkedAt = Selected.Task.LastWorkedAt
            };

            var result = _services.Tasks.Update(updated);
            result = _services.Tasks.UpdateContext(
                result.Id,
                Selected.CurrentStatus,
                Selected.LastProgress,
                Selected.NextAction,
                Selected.Blocker,
                Selected.Notes);

            if (newStatus != _loadedStatus)
            {
                result = _services.Tasks.TransitionStatus(result.Id, newStatus);
            }

            taskId = result.Id;
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == result.Id);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        LatestSnapshotSummary = string.Empty;

        if (Selected is null)
        {
            OnPropertyChanged(nameof(HasSnapshots));
            return;
        }

        var snapshots = _services.ContextSnapshots.ListByTask(Selected.Id);
        foreach (var snapshot in snapshots)
        {
            Snapshots.Add(new ContextSnapshotItemViewModel(snapshot));
        }

        var latest = snapshots.FirstOrDefault();
        if (latest is not null)
        {
            var item = new ContextSnapshotItemViewModel(latest);
            LatestSnapshotSummary = string.IsNullOrWhiteSpace(item.Summary)
                ? item.CreatedAtDisplay
                : $"{item.CreatedAtDisplay} — {item.Summary}";
        }

        OnPropertyChanged(nameof(HasSnapshots));
    }

    private void LoadFocusTime()
    {
        if (Selected is null)
        {
            FocusTimeText = string.Empty;
            OnPropertyChanged(nameof(HasFocusTime));
            return;
        }

        var focusTime = _services.Analytics.GetFocusTimeByTask(Selected.Id);
        FocusTimeText = focusTime > TimeSpan.Zero
            ? DurationFormatter.FormatFriendly(focusTime)
            : string.Empty;
        OnPropertyChanged(nameof(HasFocusTime));
    }

    private void Reopen()
    {
        if (Selected is null || !Selected.CanReopen)
        {
            return;
        }

        Message = null;
        try
        {
            var result = _services.Tasks.TransitionStatus(Selected.Id, TaskStatus.Active);
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == result.Id);
            Message = "Task reopened.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void Delete()
    {
        if (Selected is null)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            $"Delete task \"{Selected.Title}\"? This cannot be undone.",
            "Delete task",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes)
        {
            return;
        }

        Message = null;
        try
        {
            _services.Tasks.Delete(Selected.Id);
            Load();
            Message = "Task deleted.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
