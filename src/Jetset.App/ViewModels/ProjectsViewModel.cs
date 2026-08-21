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
    private MilestoneListItemViewModel? _selectedMilestone;
    private string _quickAddName = string.Empty;
    private string _quickAddTaskTitle = string.Empty;
    private string _quickAddMilestoneName = string.Empty;
    private string? _message;

    public ProjectsViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<ProjectListItemViewModel>();
        ProjectTasks = new ObservableCollection<TaskListItemViewModel>();
        Milestones = new ObservableCollection<MilestoneListItemViewModel>();

        AddProjectCommand = new RelayCommand(AddProject, CanAddProject);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        AddMilestoneCommand = new RelayCommand(AddMilestone, CanAddMilestone);
        SaveMilestoneCommand = new RelayCommand(SaveMilestone, () => SelectedMilestone is not null);
        DeleteMilestoneCommand = new RelayCommand(DeleteMilestone, () => SelectedMilestone is not null);
        MoveMilestoneUpCommand = new RelayCommand(MoveMilestoneUp, CanMoveMilestoneUp);
        MoveMilestoneDownCommand = new RelayCommand(MoveMilestoneDown, CanMoveMilestoneDown);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ProjectListItemViewModel> Items { get; }

    public ObservableCollection<TaskListItemViewModel> ProjectTasks { get; }

    public ObservableCollection<MilestoneListItemViewModel> Milestones { get; }

    public RelayCommand AddProjectCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand AddTaskCommand { get; }
    public RelayCommand AddMilestoneCommand { get; }
    public RelayCommand SaveMilestoneCommand { get; }
    public RelayCommand DeleteMilestoneCommand { get; }
    public RelayCommand MoveMilestoneUpCommand { get; }
    public RelayCommand MoveMilestoneDownCommand { get; }
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
                AddMilestoneCommand.RaiseCanExecuteChanged();
                LoadMilestones();
                LoadProjectTasks();
            }
        }
    }

    public MilestoneListItemViewModel? SelectedMilestone
    {
        get => _selectedMilestone;
        set
        {
            if (SetProperty(ref _selectedMilestone, value))
            {
                OnPropertyChanged(nameof(HasMilestoneSelection));
                SaveMilestoneCommand.RaiseCanExecuteChanged();
                DeleteMilestoneCommand.RaiseCanExecuteChanged();
                MoveMilestoneUpCommand.RaiseCanExecuteChanged();
                MoveMilestoneDownCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasMilestoneSelection => SelectedMilestone is not null;

    public bool HasItems => Items.Count > 0;

    public bool HasProjectTasks => ProjectTasks.Count > 0;

    public bool HasMilestones => Milestones.Count > 0;

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

    public string QuickAddMilestoneName
    {
        get => _quickAddMilestoneName;
        set
        {
            if (SetProperty(ref _quickAddMilestoneName, value))
            {
                AddMilestoneCommand.RaiseCanExecuteChanged();
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

    private bool CanAddMilestone() =>
        Selected is not null && !string.IsNullOrWhiteSpace(QuickAddMilestoneName);

    private bool CanMoveMilestoneUp()
    {
        if (SelectedMilestone is null)
        {
            return false;
        }

        var index = Milestones.IndexOf(SelectedMilestone);
        return index > 0;
    }

    private bool CanMoveMilestoneDown()
    {
        if (SelectedMilestone is null)
        {
            return false;
        }

        var index = Milestones.IndexOf(SelectedMilestone);
        return index >= 0 && index < Milestones.Count - 1;
    }

    private void Load()
    {
        var selectedId = Selected?.Id;
        var selectedMilestoneId = SelectedMilestone?.Id;
        Items.Clear();

        foreach (var summary in _services.Projects.List())
        {
            Items.Add(new ProjectListItemViewModel(summary));
        }

        Selected = selectedId is { } id
            ? Items.FirstOrDefault(i => i.Id == id)
            : null;

        if (Selected is not null && selectedMilestoneId is { } mid)
        {
            SelectedMilestone = Milestones.FirstOrDefault(m => m.Id == mid);
        }

        OnPropertyChanged(nameof(HasItems));
    }

    private void LoadMilestones()
    {
        var selectedMilestoneId = SelectedMilestone?.Id;
        Milestones.Clear();
        if (Selected is null)
        {
            SelectedMilestone = null;
            OnPropertyChanged(nameof(HasMilestones));
            return;
        }

        foreach (var milestone in _services.Milestones.ListByProject(Selected.Id))
        {
            var progress = _services.Milestones.GetProgress(milestone.Id);
            Milestones.Add(new MilestoneListItemViewModel(milestone, progress));
        }

        SelectedMilestone = selectedMilestoneId is { } mid
            ? Milestones.FirstOrDefault(m => m.Id == mid)
            : null;

        OnPropertyChanged(nameof(HasMilestones));
        MoveMilestoneUpCommand.RaiseCanExecuteChanged();
        MoveMilestoneDownCommand.RaiseCanExecuteChanged();
    }

    private void LoadProjectTasks()
    {
        ProjectTasks.Clear();
        if (Selected is null)
        {
            OnPropertyChanged(nameof(HasProjectTasks));
            return;
        }

        var milestoneNames = _services.Milestones.ListByProject(Selected.Id)
            .ToDictionary(m => m.Id, m => m.Name);

        foreach (var task in _services.Tasks.ListByProject(Selected.Id))
        {
            string? milestoneName = null;
            if (task.MilestoneId is { } mid)
            {
                milestoneNames.TryGetValue(mid, out milestoneName);
            }

            ProjectTasks.Add(new TaskListItemViewModel(task, Selected.Name, milestoneName));
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

    private void AddMilestone()
    {
        if (Selected is null)
        {
            return;
        }

        Message = null;
        try
        {
            var projectId = Selected.Id;
            var created = _services.Milestones.Create(projectId, QuickAddMilestoneName);
            QuickAddMilestoneName = string.Empty;
            LoadMilestones();
            LoadProjectTasks();
            SelectedMilestone = Milestones.FirstOrDefault(m => m.Id == created.Id);
            Message = "Milestone created.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void SaveMilestone()
    {
        if (SelectedMilestone is null)
        {
            return;
        }

        Message = null;
        try
        {
            var updated = new Milestone
            {
                Id = SelectedMilestone.Id,
                ProjectId = SelectedMilestone.ProjectId,
                Name = SelectedMilestone.Name,
                SortOrder = SelectedMilestone.SortOrder,
                CreatedAt = SelectedMilestone.Milestone.CreatedAt
            };

            var result = _services.Milestones.Update(updated);
            LoadMilestones();
            LoadProjectTasks();
            SelectedMilestone = Milestones.FirstOrDefault(m => m.Id == result.Id);
            Message = "Milestone updated.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void DeleteMilestone()
    {
        if (SelectedMilestone is null)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            $"Delete milestone \"{SelectedMilestone.Name}\"? Tasks will be unassigned from the milestone but not deleted.",
            "Delete milestone",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes)
        {
            return;
        }

        Message = null;
        try
        {
            _services.Milestones.Delete(SelectedMilestone.Id);
            LoadMilestones();
            LoadProjectTasks();
            Message = "Milestone deleted.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void MoveMilestoneUp()
    {
        if (Selected is null || SelectedMilestone is null)
        {
            return;
        }

        var index = Milestones.IndexOf(SelectedMilestone);
        if (index <= 0)
        {
            return;
        }

        ReorderMilestones(index, index - 1);
    }

    private void MoveMilestoneDown()
    {
        if (Selected is null || SelectedMilestone is null)
        {
            return;
        }

        var index = Milestones.IndexOf(SelectedMilestone);
        if (index < 0 || index >= Milestones.Count - 1)
        {
            return;
        }

        ReorderMilestones(index, index + 1);
    }

    private void ReorderMilestones(int fromIndex, int toIndex)
    {
        if (Selected is null)
        {
            return;
        }

        Message = null;
        try
        {
            var orderedIds = Milestones.Select(m => m.Id).ToList();
            var movedId = orderedIds[fromIndex];
            orderedIds.RemoveAt(fromIndex);
            orderedIds.Insert(toIndex, movedId);

            _services.Milestones.Reorder(Selected.Id, orderedIds);
            LoadMilestones();
            SelectedMilestone = Milestones.FirstOrDefault(m => m.Id == movedId);
            Message = "Milestone order updated.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
