using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
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
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ProjectListItemViewModel> Items { get; }

    public ObservableCollection<TaskListItemViewModel> ProjectTasks { get; }

    public RelayCommand AddProjectCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand AddTaskCommand { get; }
    public RelayCommand RefreshCommand { get; }

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

        OnPropertyChanged(nameof(HasProjectTasks));
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
            Load();
            Selected = Items.FirstOrDefault(i => i.Id == result.Id);
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
            _services.Tasks.Create(QuickAddTaskTitle, projectId);
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
