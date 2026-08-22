using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Jetset.App.Models;
using Jetset.App.ViewModels;
using WpfDataObject = System.Windows.DataObject;
using WpfDragDrop = System.Windows.DragDrop;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace Jetset.App.Views;

public partial class WorkTreeView : System.Windows.Controls.UserControl
{
    private WpfPoint _dragStartPoint;
    private bool _isDragging;

    public WorkTreeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WorkTreeViewModel oldVm)
        {
            oldVm.QuickCaptureFocusRequested -= OnQuickCaptureFocusRequested;
        }

        if (e.NewValue is WorkTreeViewModel newVm)
        {
            newVm.QuickCaptureFocusRequested += OnQuickCaptureFocusRequested;
        }
    }

    private void OnQuickCaptureFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            QuickCaptureTextBox.Focus();
            QuickCaptureTextBox.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void QuickCaptureTextBox_OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not WorkTreeViewModel viewModel)
        {
            return;
        }

        if (viewModel.QuickCaptureCommand.CanExecute(null))
        {
            viewModel.QuickCaptureCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is WorkTreeViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue as WorkTreeNodeViewModel;
        }
    }

    private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void TreeView_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (!HasExceededDragThreshold(position, _dragStartPoint))
        {
            return;
        }

        var node = GetNodeFromElement(e.OriginalSource as DependencyObject);
        if (node?.Kind != WorkItemKind.Task)
        {
            return;
        }

        _isDragging = true;
        var data = new WpfDataObject(WorkTreeDragData.Format, new WorkTreeDragData(node.Id));
        WpfDragDrop.DoDragDrop(WorkTree, data, WpfDragDropEffects.Move);
        _isDragging = false;
    }

    private void TreeView_DragOver(object sender, WpfDragEventArgs e)
    {
        if (!TryGetDragData(e, out var taskId))
        {
            e.Effects = WpfDragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (GetNodeFromElement(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        e.Effects = CanDetachToRoot(taskId) ? WpfDragDropEffects.Move : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void TreeView_Drop(object sender, WpfDragEventArgs e)
    {
        if (!TryGetDragData(e, out var taskId))
        {
            return;
        }

        if (GetNodeFromElement(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (!CanDetachToRoot(taskId))
        {
            e.Handled = true;
            return;
        }

        Reparent(taskId, null);
        e.Handled = true;
    }

    private void TreeViewItem_DragOver(object sender, WpfDragEventArgs e)
    {
        if (!TryGetDragData(e, out var taskId))
        {
            e.Effects = WpfDragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (sender is not TreeViewItem { DataContext: WorkTreeNodeViewModel target })
        {
            e.Effects = WpfDragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = target.Kind switch
        {
            WorkItemKind.Project when CanAssignToProject(taskId, target.Id) => WpfDragDropEffects.Move,
            _ => WpfDragDropEffects.None
        };
        e.Handled = true;
    }

    private void TreeViewItem_Drop(object sender, WpfDragEventArgs e)
    {
        if (!TryGetDragData(e, out var taskId))
        {
            return;
        }

        if (sender is not TreeViewItem { DataContext: WorkTreeNodeViewModel target })
        {
            return;
        }

        if (target.Kind != WorkItemKind.Project || !CanAssignToProject(taskId, target.Id))
        {
            e.Handled = true;
            return;
        }

        Reparent(taskId, target.Id);
        e.Handled = true;
    }

    private void Reparent(Guid taskId, Guid? projectId)
    {
        if (DataContext is not WorkTreeViewModel viewModel)
        {
            return;
        }

        viewModel.TryReparentTask(taskId, projectId, out _);
    }

    private void EstimateInput_OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not WorkTreeNodeViewModel node)
        {
            return;
        }

        if (DataContext is not WorkTreeViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.CommitEstimateCommand.Execute(node);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelEstimateEditCommand.Execute(node);
            e.Handled = true;
        }
    }

    private void EstimateInput_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not WorkTreeNodeViewModel node)
        {
            return;
        }

        if (!node.IsEditingEstimate)
        {
            return;
        }

        if (DataContext is WorkTreeViewModel viewModel)
        {
            viewModel.CommitEstimateCommand.Execute(node);
        }
    }

    private bool CanAssignToProject(Guid taskId, Guid projectId)
    {
        if (DataContext is not WorkTreeViewModel viewModel)
        {
            return false;
        }

        var task = viewModel.GetTask(taskId);
        return task is not null && task.ProjectId != projectId;
    }

    private bool CanDetachToRoot(Guid taskId)
    {
        if (DataContext is not WorkTreeViewModel viewModel)
        {
            return false;
        }

        var task = viewModel.GetTask(taskId);
        return task?.ProjectId is not null;
    }

    private static bool TryGetDragData(WpfDragEventArgs e, out Guid taskId)
    {
        taskId = default;
        if (!e.Data.GetDataPresent(WorkTreeDragData.Format))
        {
            return false;
        }

        if (e.Data.GetData(WorkTreeDragData.Format) is not WorkTreeDragData dragData)
        {
            return false;
        }

        taskId = dragData.TaskId;
        return true;
    }

    private static bool HasExceededDragThreshold(WpfPoint position, WpfPoint dragStartPoint) =>
        Math.Abs(position.X - dragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(position.Y - dragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private static WorkTreeNodeViewModel? GetNodeFromElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TreeViewItem { DataContext: WorkTreeNodeViewModel node })
            {
                return node;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
