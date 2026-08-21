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

public sealed class TasksViewModel : ObservableObject
{
    private readonly AppServices _services;
    private TaskListItemViewModel? _selected;
    private string _searchText = string.Empty;
    private string _quickAddTitle = string.Empty;
    private string? _message;

    public TasksViewModel(AppServices services)
    {
        _services = services;
        Items = new ObservableCollection<TaskListItemViewModel>();
        StatusOptions = Enum.GetValues<TaskStatus>();

        AddTaskCommand = new RelayCommand(AddTask, CanAddTask);
        SaveCommand = new RelayCommand(Save, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<TaskListItemViewModel> Items { get; }

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
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasItems => Items.Count > 0;

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

    private void Load()
    {
        var selectedId = Selected?.Id;
        Items.Clear();

        var tasks = string.IsNullOrWhiteSpace(SearchText)
            ? _services.Tasks.List()
            : _services.Tasks.Search(SearchText);

        foreach (var task in tasks)
        {
            Items.Add(new TaskListItemViewModel(task));
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
            var created = _services.Tasks.Create(QuickAddTitle);
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
                ProjectId = Selected.Task.ProjectId,
                MilestoneId = Selected.Task.MilestoneId,
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
