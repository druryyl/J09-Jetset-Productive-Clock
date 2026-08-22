using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class WorkTreeViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly EventHandler _sessionChangedHandler;
    private WorkTreeNodeViewModel? _selectedNode;
    private string _quickCaptureTitle = string.Empty;

    public WorkTreeViewModel(AppServices services)
    {
        _services = services;
        RootNodes = new ObservableCollection<WorkTreeNodeViewModel>();
        ContextPanel = new ContextPanelViewModel(services, RefreshAndSelect);
        RunningTaskBar = new RunningTaskBarViewModel(services);

        RefreshCommand = new RelayCommand(Load);
        ToggleExpandCommand = new RelayCommand(ToggleExpand);
        StartTaskCommand = new RelayCommand(StartTask);
        BeginEstimateEditCommand = new RelayCommand(BeginEstimateEdit);
        CommitEstimateCommand = new RelayCommand(CommitEstimate);
        CancelEstimateEditCommand = new RelayCommand(CancelEstimateEdit);
        QuickCaptureCommand = new RelayCommand(QuickCapture, CanQuickCapture);

        _sessionChangedHandler = (_, _) => Load();
        _services.Sessions.SessionChanged += _sessionChangedHandler;
        RunningTaskBar.WorkStateChanged += (_, _) => Load();

        Load();
    }

    public ObservableCollection<WorkTreeNodeViewModel> RootNodes { get; }

    public ContextPanelViewModel ContextPanel { get; }

    public RunningTaskBarViewModel RunningTaskBar { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleExpandCommand { get; }

    public RelayCommand StartTaskCommand { get; }

    public RelayCommand BeginEstimateEditCommand { get; }

    public RelayCommand CommitEstimateCommand { get; }

    public RelayCommand CancelEstimateEditCommand { get; }

    public RelayCommand QuickCaptureCommand { get; }

    public event EventHandler? QuickCaptureFocusRequested;

    public bool HasItems => RootNodes.Count > 0;

    public string QuickCaptureTitle
    {
        get => _quickCaptureTitle;
        set
        {
            if (SetProperty(ref _quickCaptureTitle, value))
            {
                QuickCaptureCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public WorkTreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                ContextPanel.ResolveFromSelection(value);
            }
        }
    }

    public bool TryReparentTask(Guid taskId, Guid? projectId, out string? error)
    {
        var task = _services.Tasks.Get(taskId);
        if (task is null)
        {
            error = "Task not found.";
            return false;
        }

        if (task.ProjectId == projectId)
        {
            error = null;
            return true;
        }

        try
        {
            if (projectId is null)
            {
                _services.Tasks.DetachFromProject(taskId);
            }
            else
            {
                _services.Tasks.AssignToProject(taskId, projectId);
                _services.TreeState.SetExpanded(projectId.Value, true);
            }

            Load();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public WorkTask? GetTask(Guid taskId) => _services.Tasks.Get(taskId);

    public void RequestQuickCaptureFocus() => QuickCaptureFocusRequested?.Invoke(this, EventArgs.Empty);

    public void SelectProject(Guid projectId)
    {
        _services.TreeState.SetExpanded(projectId, true);
        Load();

        var node = FindNode(RootNodes, projectId);
        if (node is not null && node.Kind == WorkItemKind.Project)
        {
            SelectedNode = node;
        }
    }

    public bool TryUpdateEstimate(WorkTreeNodeViewModel node, string? input, out string? error)
    {
        if (node.Kind != WorkItemKind.Task)
        {
            error = "Only tasks can have estimates.";
            return false;
        }

        if (!EffortFormatter.TryParseHours(input, out var estimateMinutes))
        {
            error = "Enter estimate as hours (e.g. 12 or 12h).";
            return false;
        }

        try
        {
            _services.Tasks.UpdateEstimate(node.Id, estimateMinutes);
            Load();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        _services.Sessions.SessionChanged -= _sessionChangedHandler;
        ContextPanel.Dispose();
        RunningTaskBar.Dispose();
    }

    private void StartTask(object? parameter)
    {
        if (parameter is not WorkTreeNodeViewModel node || node.Kind != WorkItemKind.Task)
        {
            return;
        }

        var task = _services.Tasks.Get(node.Id);
        if (task is null || !TaskStatusRules.CanStart(task.Status))
        {
            return;
        }

        try
        {
            _services.IdleAutoPause.NotifyManualResume();
            _services.WorkExecution.StartWork(node.Id);
            Load();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Start task", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
        }
    }

    private void RefreshAndSelect(Guid? nodeId, WorkItemKind? kind)
    {
        Load();

        if (nodeId is not Guid id || kind is not WorkItemKind nodeKind)
        {
            return;
        }

        var node = FindNode(RootNodes, id);
        if (node is not null && node.Kind == nodeKind)
        {
            SelectedNode = node;
        }
    }

    private void Load()
    {
        RootNodes.Clear();
        var runningTaskId = _services.Tasks.GetRunningTask()?.Id;

        foreach (var item in _services.WorkTree.ListRootWorkItems())
        {
            RootNodes.Add(CreateNode(item, runningTaskId));
        }

        OnPropertyChanged(nameof(HasItems));
        RestoreSelection();
    }

    private WorkTreeNodeViewModel CreateNode(IWorkItemNode item, Guid? runningTaskId)
    {
        var isExpanded = item.Kind == WorkItemKind.Project && _services.TreeState.IsExpanded(item.Id);
        var effortDisplay = BuildEffortDisplay(item);
        var node = new WorkTreeNodeViewModel(
            item,
            runningTaskId == item.Id,
            isExpanded,
            effortDisplay,
            OnNodeExpandedChanged);

        if (item.Kind == WorkItemKind.Project)
        {
            foreach (var child in _services.WorkTree.GetChildren(item.Id))
            {
                node.Children.Add(CreateNode(child, runningTaskId));
            }
        }

        return node;
    }

    private string BuildEffortDisplay(IWorkItemNode item) =>
        item.Kind switch
        {
            WorkItemKind.Task => BuildTaskEffortDisplay(item.Id),
            WorkItemKind.Project => BuildProjectEffortDisplay(item.Id),
            _ => string.Empty
        };

    private string BuildTaskEffortDisplay(Guid taskId)
    {
        var task = _services.Tasks.Get(taskId);
        if (task is null)
        {
            return string.Empty;
        }

        var spent = _services.Effort.GetTaskSpent(taskId);
        if (spent == TimeSpan.Zero && task.EstimateMinutes is null)
        {
            return string.Empty;
        }

        return EffortFormatter.FormatSpentEstimate(spent, task.EstimateMinutes);
    }

    private string BuildProjectEffortDisplay(Guid projectId)
    {
        var rollup = _services.Effort.GetProjectRollup(projectId);
        if (rollup.Spent == TimeSpan.Zero && rollup.EstimateMinutes is null)
        {
            return string.Empty;
        }

        return EffortFormatter.FormatSpentEstimate(rollup.Spent, rollup.EstimateMinutes);
    }

    private void BeginEstimateEdit(object? parameter)
    {
        if (parameter is not WorkTreeNodeViewModel node || node.Kind != WorkItemKind.Task)
        {
            return;
        }

        var task = _services.Tasks.Get(node.Id);
        node.BeginEstimateEdit(task?.EstimateMinutes);
    }

    private void CommitEstimate(object? parameter)
    {
        if (parameter is not WorkTreeNodeViewModel node)
        {
            return;
        }

        if (!TryUpdateEstimate(node, node.EstimateInput, out var error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                WpfMessageBox.Show(error, "Estimate", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            }

            return;
        }

        node.EndEstimateEdit();
        var restored = FindNode(RootNodes, node.Id);
        if (restored is not null)
        {
            SelectedNode = restored;
        }
    }

    private void CancelEstimateEdit(object? parameter)
    {
        if (parameter is WorkTreeNodeViewModel node)
        {
            node.EndEstimateEdit();
        }
    }

    private void OnNodeExpandedChanged(WorkTreeNodeViewModel node, bool expanded)
    {
        if (node.Kind != WorkItemKind.Project)
        {
            return;
        }

        _services.TreeState.SetExpanded(node.Id, expanded);
    }

    private void ToggleExpand(object? parameter)
    {
        if (parameter is not WorkTreeNodeViewModel node || node.Kind != WorkItemKind.Project)
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
    }

    private bool CanQuickCapture() => !string.IsNullOrWhiteSpace(QuickCaptureTitle);

    private void QuickCapture()
    {
        try
        {
            _services.Tasks.CaptureToInbox(QuickCaptureTitle);
            QuickCaptureTitle = string.Empty;
            Load();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Quick Capture", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
        }
    }

    private void RestoreSelection()
    {
        if (_selectedNode is null)
        {
            ContextPanel.ResolveFromSelection(null);
            return;
        }

        var restored = FindNode(RootNodes, _selectedNode.Id);
        if (restored is null)
        {
            _selectedNode = null;
            OnPropertyChanged(nameof(SelectedNode));
            ContextPanel.ResolveFromSelection(null);
            return;
        }

        _selectedNode = restored;
        OnPropertyChanged(nameof(SelectedNode));
        ContextPanel.ResolveFromSelection(restored);
    }

    private static WorkTreeNodeViewModel? FindNode(
        IEnumerable<WorkTreeNodeViewModel> nodes,
        Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            var childMatch = FindNode(node.Children, id);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }
}
