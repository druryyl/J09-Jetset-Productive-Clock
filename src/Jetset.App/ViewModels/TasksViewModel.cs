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
    Inbox,
    Ready,
    Running,
    Waiting,
    Done,
    Cancelled
}

public enum OriginFilterMode
{
    All,
    Planned,
    Unplanned
}

public sealed class OriginFilterOption
{
    public OriginFilterMode Mode { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
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

public sealed class TasksViewModel : ObservableObject
{
    private readonly AppServices _services;
    private TaskListItemViewModel? _selected;
    private string _searchText = string.Empty;
    private string _quickAddTitle = string.Empty;
    private ProjectFilterOption? _selectedFilter;
    private StatusFilterOption? _selectedStatusFilter;
    private OriginFilterOption? _selectedOriginFilter;
    private ProjectAssignOption? _selectedProjectOption;
    private TaskStatus _loadedStatus;
    private string? _message;
    private string _focusTimeText = string.Empty;
    private bool _suppressFilterReload;

    public TasksViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<TaskListItemViewModel>();
        FilterOptions = new ObservableCollection<ProjectFilterOption>();
        StatusFilterOptions = new ObservableCollection<StatusFilterOption>
        {
            new() { Mode = StatusFilterMode.All, DisplayName = "All statuses" },
            new() { Mode = StatusFilterMode.ActiveWork, DisplayName = "Active work" },
            new() { Mode = StatusFilterMode.Inbox, DisplayName = "Inbox" },
            new() { Mode = StatusFilterMode.Ready, DisplayName = "Ready" },
            new() { Mode = StatusFilterMode.Running, DisplayName = "Running" },
            new() { Mode = StatusFilterMode.Waiting, DisplayName = "Waiting" },
            new() { Mode = StatusFilterMode.Done, DisplayName = "Done" },
            new() { Mode = StatusFilterMode.Cancelled, DisplayName = "Cancelled" }
        };
        OriginFilterOptions = new ObservableCollection<OriginFilterOption>
        {
            new() { Mode = OriginFilterMode.All, DisplayName = "All origins" },
            new() { Mode = OriginFilterMode.Planned, DisplayName = "Planned" },
            new() { Mode = OriginFilterMode.Unplanned, DisplayName = "Unplanned" }
        };
        ProjectOptions = new ObservableCollection<ProjectAssignOption>();
        StatusOptions = Enum.GetValues<TaskStatus>();

        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        ReopenCommand = new RelayCommand(Reopen, () => Selected?.CanReopen == true);
        StartWorkCommand = new RelayCommand(BeginWork, CanExecuteStartWork);
        StartWorkForTaskCommand = new RelayCommand(BeginWorkForTask);
        ResumeWorkCommand = new RelayCommand(ResumeWork, CanExecuteResumeWork);
        ResumeWorkForTaskCommand = new RelayCommand(ResumeWorkForTask);
        SwitchAndMarkWaitingCommand = new RelayCommand(SwitchAndMarkWaiting, CanExecuteSwitchAndMarkWaiting);
        SwitchAndMarkWaitingForTaskCommand = new RelayCommand(SwitchAndMarkWaitingForTask);
        SetStatusFilterCommand = new RelayCommand(SetStatusFilter);
        ViewProjectContextCommand = new RelayCommand(ViewProjectContext, () => Selected?.HasProject == true);
        RefreshCommand = new RelayCommand(Load);

        RebuildProjectOptions();
        _selectedFilter = FilterOptions[0];
        _selectedStatusFilter = StatusFilterOptions.FirstOrDefault(o => o.Mode == StatusFilterMode.Inbox)
            ?? StatusFilterOptions[0];
        _selectedOriginFilter = OriginFilterOptions[0];
        Load();
    }

    public ObservableCollection<TaskListItemViewModel> Items { get; }

    public ObservableCollection<ProjectFilterOption> FilterOptions { get; }

    public ObservableCollection<StatusFilterOption> StatusFilterOptions { get; }

    public ObservableCollection<OriginFilterOption> OriginFilterOptions { get; }

    public ObservableCollection<ProjectAssignOption> ProjectOptions { get; }

    public TaskStatus[] StatusOptions { get; }

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ReopenCommand { get; }
    public RelayCommand StartWorkCommand { get; }
    public RelayCommand StartWorkForTaskCommand { get; }
    public RelayCommand ResumeWorkCommand { get; }
    public RelayCommand ResumeWorkForTaskCommand { get; }
    public RelayCommand SwitchAndMarkWaitingCommand { get; }
    public RelayCommand SwitchAndMarkWaitingForTaskCommand { get; }
    public RelayCommand SetStatusFilterCommand { get; }
    public RelayCommand ViewProjectContextCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public event EventHandler? WorkStarted;
    public event EventHandler<Guid>? ViewProjectContextRequested;

    public TaskListItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                _loadedStatus = value?.Status ?? TaskStatus.Ready;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanReopenSelected));
                OnPropertyChanged(nameof(CanStartWorkSelected));
                OnPropertyChanged(nameof(CanResumeWorkSelected));
                OnPropertyChanged(nameof(CanSwitchWorkSelected));
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                ReopenCommand.RaiseCanExecuteChanged();
                StartWorkCommand.RaiseCanExecuteChanged();
                ResumeWorkCommand.RaiseCanExecuteChanged();
                SwitchAndMarkWaitingCommand.RaiseCanExecuteChanged();
                ViewProjectContextCommand.RaiseCanExecuteChanged();
                SyncSelectedProjectOption();
                LoadFocusTime();
                OnPropertyChanged(nameof(EditableStatusOptions));
            }
        }
    }

    public bool CanSwitchTasks => _services.Sessions.ActiveSession is not null;

    public bool IsInboxFilterSelected => SelectedStatusFilter?.Mode == StatusFilterMode.Inbox;

    public bool IsReadyFilterSelected => SelectedStatusFilter?.Mode == StatusFilterMode.Ready;

    public bool IsWaitingFilterSelected => SelectedStatusFilter?.Mode == StatusFilterMode.Waiting;

    public bool IsDoneFilterSelected => SelectedStatusFilter?.Mode == StatusFilterMode.Done;

    public bool IsAllStatusesFilterSelected => SelectedStatusFilter?.Mode == StatusFilterMode.All;

    public TaskStatus[] EditableStatusOptions =>
        Selected?.Status == TaskStatus.Running
            ? [TaskStatus.Running, TaskStatus.Ready, TaskStatus.Waiting, TaskStatus.Done, TaskStatus.Cancelled]
            : StatusOptions.Where(s => s != TaskStatus.Running).ToArray();

    public string FocusTimeText
    {
        get => _focusTimeText;
        private set => SetProperty(ref _focusTimeText, value);
    }

    public bool HasFocusTime => !string.IsNullOrWhiteSpace(FocusTimeText);

    public bool HasSelection => Selected is not null;

    public bool HasItems => Items.Count > 0;

    public bool CanReopenSelected => Selected?.CanReopen == true;

    public bool CanResumeWorkSelected => Selected?.CanResumeWork == true;

    public bool CanStartWorkSelected => Selected?.CanStartWork == true;

    public bool CanSwitchWorkSelected => CanSwitchTasks && CanStartWorkSelected;

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

    public OriginFilterOption? SelectedOriginFilter
    {
        get => _selectedOriginFilter;
        set
        {
            if (SetProperty(ref _selectedOriginFilter, value) && !_suppressFilterReload)
            {
                Load();
            }
        }
    }

    public ProjectAssignOption? SelectedProjectOption
    {
        get => _selectedProjectOption;
        set => SetProperty(ref _selectedProjectOption, value);
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

    private void SyncSelectedProjectOption()
    {
        if (Selected is null)
        {
            SelectedProjectOption = null;
            return;
        }

        SelectedProjectOption = ProjectOptions.FirstOrDefault(p => p.ProjectId == Selected.ProjectId)
            ?? ProjectOptions[0];
    }

    private Dictionary<Guid, string> BuildProjectNameMap() =>
        _services.Projects.ListProjects().ToDictionary(p => p.Id, p => p.Name);

    private static bool MatchesOriginFilter(WorkTask task, OriginFilterMode mode) =>
        mode switch
        {
            OriginFilterMode.All => true,
            OriginFilterMode.Planned => task.Origin == TaskOrigin.Planned,
            OriginFilterMode.Unplanned => task.Origin == TaskOrigin.Unplanned,
            _ => true
        };

    private static bool MatchesStatusFilter(WorkTask task, StatusFilterMode mode) =>
        mode switch
        {
            StatusFilterMode.All => true,
            StatusFilterMode.ActiveWork => TaskStatusRules.IsEligibleForActiveWork(task.Status),
            StatusFilterMode.Inbox => task.Status == TaskStatus.Inbox,
            StatusFilterMode.Ready => task.Status == TaskStatus.Ready,
            StatusFilterMode.Running => task.Status == TaskStatus.Running,
            StatusFilterMode.Waiting => task.Status == TaskStatus.Waiting,
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

        var originMode = SelectedOriginFilter?.Mode ?? OriginFilterMode.All;
        tasks = tasks.Where(t => MatchesOriginFilter(t, originMode));

        foreach (var task in tasks)
        {
            string? projectName = null;
            if (task.ProjectId is { } pid)
            {
                projectNames.TryGetValue(pid, out projectName);
            }

            Items.Add(new TaskListItemViewModel(task, projectName));
        }

        ApplyWorkSessionState();

        Selected = selectedId is { } id
            ? Items.FirstOrDefault(i => i.Id == id)
            : null;

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsInboxFilterSelected));
        OnPropertyChanged(nameof(IsReadyFilterSelected));
        OnPropertyChanged(nameof(IsWaitingFilterSelected));
        OnPropertyChanged(nameof(IsDoneFilterSelected));
        OnPropertyChanged(nameof(IsAllStatusesFilterSelected));
        OnPropertyChanged(nameof(CanSwitchTasks));
        OnPropertyChanged(nameof(CanSwitchWorkSelected));
        StartWorkCommand.RaiseCanExecuteChanged();
        ResumeWorkCommand.RaiseCanExecuteChanged();
        SwitchAndMarkWaitingCommand.RaiseCanExecuteChanged();
    }

    private void ApplyWorkSessionState()
    {
        var execution = _services.WorkExecution;
        foreach (var item in Items)
        {
            item.HasPausedSession = execution.HasPausedSession(item.Id);
            item.IsActiveSession = execution.IsTaskFocused(item.Id);
            item.ShowSwitchActions = CanSwitchTasks && item.CanStartWork;
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
            _services.WorkExecution.StartWork(taskId);
            Load();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work started.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private bool CanExecuteSwitchAndMarkWaiting() =>
        CanSwitchTasks && Selected?.CanStartWork == true;

    private void SwitchAndMarkWaiting()
    {
        if (Selected is null)
        {
            return;
        }

        SwitchAndMarkWaitingForTask(Selected.Id);
    }

    private void SwitchAndMarkWaitingForTask(object? parameter)
    {
        if (!TryParseTaskId(parameter, out var taskId))
        {
            return;
        }

        Message = null;
        try
        {
            _services.WorkExecution.StartWork(taskId, leavingStatus: TaskStatus.Waiting);
            Load();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Switched — previous task marked waiting.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void SetStatusFilter(object? parameter)
    {
        if (parameter is not StatusFilterMode mode)
        {
            return;
        }

        var option = StatusFilterOptions.FirstOrDefault(o => o.Mode == mode)
            ?? StatusFilterOptions.First();

        SelectedStatusFilter = option;
    }

    private void ViewProjectContext()
    {
        if (Selected?.ProjectId is not { } projectId)
        {
            return;
        }

        ViewProjectContextRequested?.Invoke(this, projectId);
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
            _services.WorkExecution.ResumeWork(taskId);
            Load();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work resumed.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
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

            var created = _services.Tasks.CaptureToInbox(QuickAddTitle, projectId);
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
            var newStatus = Selected.Status;

            var updated = new WorkTask
            {
                Id = Selected.Id,
                Title = Selected.Title,
                Status = Selected.Status,
                Notes = Selected.Notes,
                ProjectId = projectId,
                CreatedAt = Selected.Task.CreatedAt,
                UpdatedAt = Selected.Task.UpdatedAt,
                LastWorkedAt = Selected.Task.LastWorkedAt
            };

            var result = _services.Tasks.Update(updated);

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
            var result = _services.Tasks.TransitionStatus(Selected.Id, TaskStatus.Ready);
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
