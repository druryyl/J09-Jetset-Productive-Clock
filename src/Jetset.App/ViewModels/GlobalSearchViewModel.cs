using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class GlobalSearchViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _searchText = string.Empty;

    public GlobalSearchViewModel(AppServices services)
    {
        _services = services;
        Results = new ObservableCollection<TaskListItemViewModel>();

        StartWorkForTaskCommand = new RelayCommand(BeginWorkForTask);
        ResumeWorkForTaskCommand = new RelayCommand(ResumeWorkForTask);
        ClearSearchCommand = new RelayCommand(ClearSearch);
    }

    public ObservableCollection<TaskListItemViewModel> Results { get; }

    public RelayCommand StartWorkForTaskCommand { get; }

    public RelayCommand ResumeWorkForTaskCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public event EventHandler? WorkStarted;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RunSearch();
                OnPropertyChanged(nameof(ShowResults));
            }
        }
    }

    public bool ShowResults => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasResults => Results.Count > 0;

    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void RunSearch()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            OnPropertyChanged(nameof(HasResults));
            return;
        }

        var projectNames = _services.Projects.ListProjects().ToDictionary(p => p.Id, p => p.Name);

        foreach (var task in _services.Tasks.Search(SearchText))
        {
            string? projectName = null;
            if (task.ProjectId is { } pid)
            {
                projectNames.TryGetValue(pid, out projectName);
            }

            Results.Add(new TaskListItemViewModel(task, projectName));
        }

        ApplyWorkSessionState();
        OnPropertyChanged(nameof(HasResults));
    }

    private void ApplyWorkSessionState()
    {
        var execution = _services.WorkExecution;
        foreach (var item in Results)
        {
            item.HasPausedSession = execution.HasPausedSession(item.Id);
            item.IsActiveSession = execution.IsTaskFocused(item.Id);
        }
    }

    private void BeginWorkForTask(object? parameter)
    {
        if (!TryParseTaskId(parameter, out var taskId))
        {
            return;
        }

        try
        {
            _services.WorkExecution.StartWork(taskId);
            RunSearch();
            WorkStarted?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Search is read-only; errors surface in Focus after navigation.
        }
    }

    private void ResumeWorkForTask(object? parameter)
    {
        if (!TryParseTaskId(parameter, out var taskId))
        {
            return;
        }

        try
        {
            _services.WorkExecution.ResumeWork(taskId);
            RunSearch();
            WorkStarted?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Search is read-only; errors surface in Focus after navigation.
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
}
