using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class MilestoneListItemViewModel : ObservableObject
{
    private string _name;
    private string _progressText;
    private int _sortOrder;

    public MilestoneListItemViewModel(Milestone milestone, MilestoneProgress progress)
    {
        Milestone = milestone;
        _name = milestone.Name;
        _sortOrder = milestone.SortOrder;
        _progressText = FormatProgress(progress);
    }

    public Milestone Milestone { get; }

    public Guid Id => Milestone.Id;

    public Guid ProjectId => Milestone.ProjectId;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public void ApplyProgress(MilestoneProgress progress)
    {
        ProgressText = FormatProgress(progress);
    }

    private static string FormatProgress(MilestoneProgress progress) =>
        $"{progress.DoneCount} / {progress.TotalCount} done";
}
