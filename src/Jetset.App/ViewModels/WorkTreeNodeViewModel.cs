using System.Collections.ObjectModel;
using Jetset.App.Helpers;

using Jetset.App.Models;



namespace Jetset.App.ViewModels;



public sealed class WorkTreeNodeViewModel : ObservableObject

{

    private bool _isExpanded;

    private bool _isEditingEstimate;

    private string _estimateInput = string.Empty;

    private string _effortDisplayText = string.Empty;

    private bool _showEffort;

    private Action<WorkTreeNodeViewModel, bool>? _onExpandedChanged;



    public WorkTreeNodeViewModel(

        IWorkItemNode node,

        bool isRunning,

        bool isExpanded,

        string effortDisplayText,

        Action<WorkTreeNodeViewModel, bool>? onExpandedChanged = null)

    {

        Id = node.Id;

        Kind = node.Kind;

        Title = node.DisplayName;

        ParentProjectId = node.ParentProjectId;

        IsRunning = isRunning;

        _isExpanded = isExpanded;

        _effortDisplayText = effortDisplayText;

        _showEffort = !string.IsNullOrEmpty(effortDisplayText);

        _onExpandedChanged = onExpandedChanged;

        Children = new ObservableCollection<WorkTreeNodeViewModel>();

    }



    public Guid Id { get; }



    public WorkItemKind Kind { get; }



    public string Title { get; }



    public Guid? ParentProjectId { get; }



    public bool IsRunning { get; }



    public bool IsProject => Kind == WorkItemKind.Project;



    public bool IsTask => Kind == WorkItemKind.Task;



    public ObservableCollection<WorkTreeNodeViewModel> Children { get; }



    public string EffortDisplayText

    {

        get => _effortDisplayText;

        private set

        {

            if (SetProperty(ref _effortDisplayText, value))

            {

                ShowEffort = !string.IsNullOrEmpty(value);

            }

        }

    }



    public bool ShowEffort

    {

        get => _showEffort;

        private set => SetProperty(ref _showEffort, value);

    }



    public bool IsEditingEstimate

    {

        get => _isEditingEstimate;

        set => SetProperty(ref _isEditingEstimate, value);

    }



    public string EstimateInput

    {

        get => _estimateInput;

        set => SetProperty(ref _estimateInput, value);

    }



    public bool IsExpanded

    {

        get => _isExpanded;

        set

        {

            if (SetProperty(ref _isExpanded, value))

            {

                _onExpandedChanged?.Invoke(this, value);

            }

        }

    }



    internal void SetExpandedSilently(bool expanded)

    {

        if (_isExpanded == expanded)

        {

            return;

        }



        _isExpanded = expanded;

        OnPropertyChanged(nameof(IsExpanded));

    }



    internal void SetEffortDisplay(string effortDisplayText)

    {

        EffortDisplayText = effortDisplayText;

    }



    internal void BeginEstimateEdit(int? estimateMinutes)

    {

        EstimateInput = estimateMinutes is int minutes

            ? ((int)Math.Round(TimeSpan.FromMinutes(minutes).TotalHours, MidpointRounding.AwayFromZero)).ToString()

            : string.Empty;

        IsEditingEstimate = true;

    }



    internal void EndEstimateEdit()

    {

        IsEditingEstimate = false;

        EstimateInput = string.Empty;

    }

}

