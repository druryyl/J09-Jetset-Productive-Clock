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

public sealed class ProjectsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private ProjectListItemViewModel? _selected;
    private string _quickAddName = string.Empty;
    private string _quickAddTaskTitle = string.Empty;
    private string? _message;

    public ProjectsViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<ProjectListItemViewModel>();
        ProjectTasks = new ObservableCollection<TaskListItemViewModel>();

        AddProjectCommand = new RelayCommand(AddProject, CanAddProject);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        StartWorkForTaskCommand = new RelayCommand(BeginWorkForTask);
        ResumeWorkForTaskCommand = new RelayCommand(ResumeWorkForTask);
        SwitchAndMarkWaitingForTaskCommand = new RelayCommand(SwitchAndMarkWaitingForTask);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ProjectListItemViewModel> Items { get; }

    public ObservableCollection<TaskListItemViewModel> ProjectTasks { get; }

    public RelayCommand AddProjectCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand AddTaskCommand { get; }
    public RelayCommand StartWorkForTaskCommand { get; }
    public RelayCommand ResumeWorkForTaskCommand { get; }
    public RelayCommand SwitchAndMarkWaitingForTaskCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public event EventHandler? WorkStarted;

    public bool CanSwitchTasks => _services.Sessions.ActiveSession is not null;

    public void SelectProject(Guid projectId)
    {
        if (Items.All(i => i.Id != projectId))
        {
            Load();
        }

        Selected = Items.FirstOrDefault(i => i.Id == projectId);
    }

    public ProjectListItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                AddTaskCommand.RaiseCanExecuteChanged();
                LoadProjectTasks();
                LoadSelectedContext();
                OnPropertyChanged(nameof(CanSwitchTasks));
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasItems => Items.Count > 0;

    public bool HasProjectTasks => ProjectTasks.Count > 0;

    public string QuickAddName
    {
        get => _quickAddName;
        set
        {
            if (SetProperty(ref _quickAddName, value))
            {
                AddProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuickAddTaskTitle
    {
        get => _quickAddTaskTitle;
        set
        {
            if (SetProperty(ref _quickAddTaskTitle, value))
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

    private bool CanAddProject() => !string.IsNullOrWhiteSpace(QuickAddName);

    private bool CanAddTask() => Selected is not null && !string.IsNullOrWhiteSpace(QuickAddTaskTitle);

    private void Load()
    {
        var selectedId = Selected?.Id;
        Items.Clear();

        foreach (var summary in _services.Projects.List())
        {
            Items.Add(new ProjectListItemViewModel(summary));
        }

        Selected = selectedId is { } id
            ? Items.FirstOrDefault(i => i.Id == id)
            : null;

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CanSwitchTasks));
    }

    private void LoadProjectTasks()
    {
        ProjectTasks.Clear();
        if (Selected is null)
        {
            OnPropertyChanged(nameof(HasProjectTasks));
            return;
        }

        foreach (var task in _services.Tasks.ListByProject(Selected.Id))
        {
            ProjectTasks.Add(new TaskListItemViewModel(task, Selected.Name));
        }

        ApplyWorkSessionState();
        OnPropertyChanged(nameof(HasProjectTasks));
    }

    private void LoadSelectedContext()
    {
        if (Selected is null)
        {
            return;
        }

        var project = _services.Projects.Get(Selected.Id);
        if (project is not null)
        {
            Selected.ContextText = project.ContextText ?? string.Empty;
        }
    }

    private void ApplyWorkSessionState()
    {
        var execution = _services.WorkExecution;
        foreach (var item in ProjectTasks)
        {
            item.HasPausedSession = execution.HasPausedSession(item.Id);
            item.IsActiveSession = execution.IsTaskFocused(item.Id);
            item.ShowSwitchActions = CanSwitchTasks && item.CanStartWork;
        }
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
            LoadProjectTasks();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work started.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
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
            LoadProjectTasks();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Work resumed.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
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
            LoadProjectTasks();
            WorkStarted?.Invoke(this, EventArgs.Empty);
            Message = "Switched — previous task marked waiting.";
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

    private void AddProject()
    {
        Message = null;
        try
        {
            var created = _services.Projects.Create(QuickAddName);
            QuickAddName = string.Empty;
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == created.Id);
            Message = "Project created.";
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
            var updated = new Project
            {
                Id = Selected.Id,
                Name = Selected.Name,
                Deadline = Selected.Deadline,
                CreatedAt = Selected.Project.CreatedAt,
                UpdatedAt = Selected.Project.UpdatedAt
            };

            var result = _services.Projects.Update(updated);
            _services.Projects.UpdateContextText(Selected.Id, Selected.ContextText);

            Load();
            Selected = Items.FirstOrDefault(i => i.Id == result.Id);
            LoadSelectedContext();
            Message = "Project updated.";
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
            $"Delete project \"{Selected.Name}\"? Tasks will be unassigned but not deleted.",
            "Delete project",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes)
        {
            return;
        }

        Message = null;
        try
        {
            _services.Projects.Delete(Selected.Id);
            Load();
            Message = "Project deleted.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void AddTask()
    {
        if (Selected is null)
        {
            return;
        }

        Message = null;
        try
        {
            var projectId = Selected.Id;
            var created = _services.Tasks.Create(QuickAddTaskTitle, projectId, TaskOrigin.Planned);
            QuickAddTaskTitle = string.Empty;
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == projectId);
            Message = "Task added to project.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
