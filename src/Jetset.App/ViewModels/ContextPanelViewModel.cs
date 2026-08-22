using System.Globalization;
using System.Windows.Threading;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace Jetset.App.ViewModels;

/// <summary>
/// Project context panel — context text, deadline, effort rollup, and conversion actions.
/// </summary>
public sealed class ContextPanelViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly Action<Guid?, WorkItemKind?> _onTreeRefresh;
    private readonly DispatcherTimer _contextSaveTimer;
    private WorkTreeNodeViewModel? _selectedNode;
    private Guid? _resolvedProjectId;
    private bool _isLoading;
    private string _projectName = string.Empty;
    private string _contextText = string.Empty;
    private bool _hasDeadline;
    private DateTime? _deadlineDate;
    private string _spentText = string.Empty;
    private string _estimateText = string.Empty;
    private bool _hasEstimate;

    public ContextPanelViewModel(AppServices services, Action<Guid?, WorkItemKind?> onTreeRefresh)
    {
        _services = services;
        _onTreeRefresh = onTreeRefresh;

        _contextSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _contextSaveTimer.Tick += OnContextSaveTimerTick;

        ConvertToProjectCommand = new RelayCommand(ConvertToProject, () => CanConvertToProject);
        ConvertToTaskCommand = new RelayCommand(ConvertToTask, () => CanConvertToTask);
    }

    public bool IsVisible { get; private set; }

    public string ProjectName
    {
        get => _projectName;
        private set => SetProperty(ref _projectName, value);
    }

    public string ContextText
    {
        get => _contextText;
        set
        {
            if (SetProperty(ref _contextText, value) && !_isLoading)
            {
                ScheduleContextSave();
            }
        }
    }

    public bool HasDeadline
    {
        get => _hasDeadline;
        set
        {
            if (SetProperty(ref _hasDeadline, value))
            {
                OnPropertyChanged(nameof(DeadlineText));
                if (!_isLoading)
                {
                    if (!value)
                    {
                        DeadlineDate = null;
                    }
                    else if (DeadlineDate is null)
                    {
                        DeadlineDate = DateTime.Today;
                    }

                    SaveDeadline();
                }
            }
        }
    }

    public DateTime? DeadlineDate
    {
        get => _deadlineDate;
        set
        {
            if (SetProperty(ref _deadlineDate, value))
            {
                OnPropertyChanged(nameof(DeadlineText));
                if (!_isLoading && HasDeadline)
                {
                    SaveDeadline();
                }
            }
        }
    }

    public string DeadlineText =>
        HasDeadline && DeadlineDate is { } date
            ? date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            : string.Empty;

    public string SpentText
    {
        get => _spentText;
        private set => SetProperty(ref _spentText, value);
    }

    public string EstimateText
    {
        get => _estimateText;
        private set => SetProperty(ref _estimateText, value);
    }

    public bool HasEstimate
    {
        get => _hasEstimate;
        private set => SetProperty(ref _hasEstimate, value);
    }

    public string EffortSummaryText =>
        HasEstimate ? $"{SpentText} / {EstimateText}" : SpentText;

    public Guid? ResolvedProjectId => _resolvedProjectId;

    public bool CanConvertToProject =>
        _selectedNode is { Kind: WorkItemKind.Task } node
        && _services.WorkItemConversion.CanConvertTaskToProject(node.Id);

    public bool CanConvertToTask =>
        _selectedNode is { Kind: WorkItemKind.Project } node
        && _services.WorkItemConversion.CanConvertProjectToTask(node.Id);

    public RelayCommand ConvertToProjectCommand { get; }

    public RelayCommand ConvertToTaskCommand { get; }

    public void ResolveFromSelection(WorkTreeNodeViewModel? node)
    {
        _selectedNode = node;

        if (node is null)
        {
            Clear();
            return;
        }

        var projectId = node.Kind switch
        {
            WorkItemKind.Project => node.Id,
            WorkItemKind.Task when node.ParentProjectId is Guid parentId => parentId,
            _ => (Guid?)null
        };

        if (projectId is null)
        {
            Clear();
            return;
        }

        var project = _services.Projects.Get(projectId.Value);
        if (project is null)
        {
            Clear();
            return;
        }

        _isLoading = true;
        try
        {
            _resolvedProjectId = project.Id;
            ProjectName = project.Name;
            ContextText = project.ContextText ?? string.Empty;
            HasDeadline = project.Deadline is not null;
            DeadlineDate = project.Deadline?.ToDateTime(TimeOnly.MinValue);
            LoadRollup(project.Id);
            SetIsVisible(true);
        }
        finally
        {
            _isLoading = false;
        }

        RaiseConversionStateChanged();
    }

    public void Dispose()
    {
        _contextSaveTimer.Stop();
        _contextSaveTimer.Tick -= OnContextSaveTimerTick;
    }

    private void Clear()
    {
        _contextSaveTimer.Stop();
        _selectedNode = null;
        _resolvedProjectId = null;
        ProjectName = string.Empty;
        ContextText = string.Empty;
        HasDeadline = false;
        DeadlineDate = null;
        SpentText = string.Empty;
        EstimateText = string.Empty;
        HasEstimate = false;
        SetIsVisible(false);
        RaiseConversionStateChanged();
    }

    private void LoadRollup(Guid projectId)
    {
        var rollup = _services.Effort.GetProjectRollup(projectId);
        SpentText = EffortFormatter.FormatHours(rollup.Spent);
        HasEstimate = rollup.EstimateMinutes is not null;
        EstimateText = rollup.EstimateMinutes is int minutes
            ? EffortFormatter.FormatHours(minutes)
            : string.Empty;
        OnPropertyChanged(nameof(EffortSummaryText));
    }

    private void ScheduleContextSave()
    {
        _contextSaveTimer.Stop();
        _contextSaveTimer.Start();
    }

    private void OnContextSaveTimerTick(object? sender, EventArgs e)
    {
        _contextSaveTimer.Stop();
        SaveContext();
    }

    private void SaveContext()
    {
        if (_resolvedProjectId is not Guid projectId)
        {
            return;
        }

        try
        {
            _services.Projects.UpdateContextText(projectId, ContextText);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Could not save context",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
        }
    }

    private void SaveDeadline()
    {
        if (_resolvedProjectId is not Guid projectId)
        {
            return;
        }

        var existing = _services.Projects.Get(projectId);
        if (existing is null)
        {
            return;
        }

        try
        {
            DateOnly? deadline = null;
            if (HasDeadline && DeadlineDate is { } date)
            {
                deadline = DateOnly.FromDateTime(date);
            }

            var updated = new Project
            {
                Id = existing.Id,
                Name = existing.Name,
                Deadline = deadline,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt
            };

            _services.Projects.Update(updated);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Could not save deadline",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
        }
    }

    private void ConvertToProject()
    {
        if (_selectedNode is not { Kind: WorkItemKind.Task } node)
        {
            return;
        }

        try
        {
            var project = _services.WorkItemConversion.ConvertTaskToProject(node.Id);
            _onTreeRefresh(project.Id, WorkItemKind.Project);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Convert to project",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
        }
    }

    private void ConvertToTask()
    {
        if (_selectedNode is not { Kind: WorkItemKind.Project } node)
        {
            return;
        }

        var info = _services.WorkItemConversion.GetProjectToTaskInfo(node.Id);

        if (info.HasDeadline)
        {
            var proceed = WpfMessageBox.Show(
                "Converting this project to a task will remove its deadline. Continue?",
                "Convert to task",
                WpfMessageBoxButton.YesNo,
                WpfMessageBoxImage.Warning);

            if (proceed != WpfMessageBoxResult.Yes)
            {
                return;
            }
        }

        var transferContext = false;
        if (info.HasContext)
        {
            var transfer = WpfMessageBox.Show(
                "Transfer project context to the new task's notes?",
                "Convert to task",
                WpfMessageBoxButton.YesNo,
                WpfMessageBoxImage.Question);

            transferContext = transfer == WpfMessageBoxResult.Yes;
        }

        try
        {
            var task = _services.WorkItemConversion.ConvertProjectToTask(node.Id, transferContext);
            _onTreeRefresh(task.Id, WorkItemKind.Task);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Convert to task",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
        }
    }

    private void RaiseConversionStateChanged()
    {
        OnPropertyChanged(nameof(CanConvertToProject));
        OnPropertyChanged(nameof(CanConvertToTask));
        ConvertToProjectCommand.RaiseCanExecuteChanged();
        ConvertToTaskCommand.RaiseCanExecuteChanged();
    }

    private void SetIsVisible(bool visible)
    {
        if (IsVisible == visible)
        {
            return;
        }

        IsVisible = visible;
        OnPropertyChanged(nameof(IsVisible));
    }
}
