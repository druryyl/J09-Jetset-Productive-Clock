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

public sealed class ProjectFilterOption
{
    public ProjectFilterMode Mode { get; init; }

    public Guid? ProjectId { get; init; }

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
    private ProjectAssignOption? _selectedProjectOption;
    private MilestoneAssignOption? _selectedMilestoneOption;
    private string? _message;
    private bool _suppressFilterReload;
    private bool _suppressMilestoneRebuild;

    public TasksViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<TaskListItemViewModel>();
        FilterOptions = new ObservableCollection<ProjectFilterOption>();
        ProjectOptions = new ObservableCollection<ProjectAssignOption>();
        MilestoneOptions = new ObservableCollection<MilestoneAssignOption>();
        StatusOptions = Enum.GetValues<TaskStatus>();

        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        RefreshCommand = new RelayCommand(Load);

        RebuildProjectOptions();
        _selectedFilter = FilterOptions[0];
        Load();
    }

    public ObservableCollection<TaskListItemViewModel> Items { get; }

    public ObservableCollection<ProjectFilterOption> FilterOptions { get; }

    public ObservableCollection<ProjectAssignOption> ProjectOptions { get; }

    public ObservableCollection<MilestoneAssignOption> MilestoneOptions { get; }

    public TaskStatus[] StatusOptions { get; }

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public TaskListItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                SyncSelectedProjectOption();
                RebuildMilestoneOptions(SelectedProjectOption?.ProjectId);
                SyncSelectedMilestoneOption();
                OnPropertyChanged(nameof(CanAssignMilestone));
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasItems => Items.Count > 0;

    public bool CanAssignMilestone => SelectedProjectOption?.ProjectId is not null;

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

        Selected = selectedId is { } id
            ? Items.FirstOrDefault(i => i.Id == id)
            : null;

        OnPropertyChanged(nameof(HasItems));
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
        if (Selected is null)
        {
            return;
        }

        Message = null;
        try
        {
            var projectId = SelectedProjectOption?.ProjectId;
            var milestoneId = projectId is null ? null : SelectedMilestoneOption?.MilestoneId;

            var updated = new WorkTask
            {
                Id = Selected.Id,
                Title = Selected.Title,
                Status = Selected.Status,
                Notes = Selected.Notes,
                CurrentStatus = Selected.Task.CurrentStatus,
                LastProgress = Selected.Task.LastProgress,
                NextAction = Selected.Task.NextAction,
                Blocker = Selected.Task.Blocker,
                ProjectId = projectId,
                MilestoneId = milestoneId,
                CreatedAt = Selected.Task.CreatedAt,
                UpdatedAt = Selected.Task.UpdatedAt,
                LastWorkedAt = Selected.Task.LastWorkedAt
            };

            var result = _services.Tasks.Update(updated);
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == result.Id);
            Message = "Task updated.";
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
