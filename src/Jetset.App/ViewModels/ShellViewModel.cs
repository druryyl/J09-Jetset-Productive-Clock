using Jetset.App.Helpers;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    public const double FocusDefaultWidth = 360;
    public const double FocusDefaultHeight = 480;
    public const double FocusCompactMinWidth = 280;
    public const double FocusCompactMinHeight = 160;
    public const double PlanningMinWidth = 720;
    public const double PlanningMinHeight = 560;

    private ShellArea _currentArea = ShellArea.Focus;
    private bool _showNavigation = true;

    public ShellViewModel(AppServices services)
    {
        Focus = new FocusViewModel(services);
        Tasks = new TasksViewModel(services);
        Projects = new ProjectsViewModel(services);
        Analytics = new AnalyticsViewModel(services);
        Search = new GlobalSearchViewModel(services);

        NavigateFocusCommand = new RelayCommand(() => NavigateTo(ShellArea.Focus));
        NavigateTasksCommand = new RelayCommand(() => NavigateTo(ShellArea.Tasks));
        NavigateProjectsCommand = new RelayCommand(() => NavigateTo(ShellArea.Projects));
        NavigateAnalyticsCommand = new RelayCommand(() => NavigateTo(ShellArea.Analytics));

        Focus.CompactModeChanged += OnFocusCompactModeChanged;
        Focus.EditProjectContextRequested += (_, projectId) => NavigateToProject(projectId);
        Tasks.WorkStarted += (_, _) => NavigateTo(ShellArea.Focus);
        Tasks.ViewProjectContextRequested += (_, projectId) => NavigateToProject(projectId);
        Projects.WorkStarted += (_, _) => NavigateTo(ShellArea.Focus);
        Search.WorkStarted += (_, _) => NavigateTo(ShellArea.Focus);
        UpdateShowNavigation();
        RaiseWindowSizeHint();
    }

    public FocusViewModel Focus { get; }

    public TasksViewModel Tasks { get; }

    public ProjectsViewModel Projects { get; }

    public AnalyticsViewModel Analytics { get; }

    public GlobalSearchViewModel Search { get; }

    public RelayCommand NavigateFocusCommand { get; }

    public RelayCommand NavigateTasksCommand { get; }

    public RelayCommand NavigateProjectsCommand { get; }

    public RelayCommand NavigateAnalyticsCommand { get; }

    public event EventHandler? WindowSizeHintChanged;

    public ShellArea CurrentArea
    {
        get => _currentArea;
        private set
        {
            if (SetProperty(ref _currentArea, value))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
                OnPropertyChanged(nameof(IsFocusSelected));
                OnPropertyChanged(nameof(IsTasksSelected));
                OnPropertyChanged(nameof(IsProjectsSelected));
                OnPropertyChanged(nameof(IsAnalyticsSelected));
                UpdateShowNavigation();
                RaiseWindowSizeHint();
            }
        }
    }

    public object CurrentViewModel => CurrentArea switch
    {
        ShellArea.Tasks => Tasks,
        ShellArea.Projects => Projects,
        ShellArea.Analytics => Analytics,
        _ => Focus
    };

    public bool ShowNavigation
    {
        get => _showNavigation;
        private set => SetProperty(ref _showNavigation, value);
    }

    public bool IsFocusSelected => CurrentArea == ShellArea.Focus;

    public bool IsTasksSelected => CurrentArea == ShellArea.Tasks;

    public bool IsProjectsSelected => CurrentArea == ShellArea.Projects;

    public bool IsAnalyticsSelected => CurrentArea == ShellArea.Analytics;

    public double SuggestedMinWidth =>
        CurrentArea == ShellArea.Focus ? FocusCompactMinWidth : PlanningMinWidth;

    public double SuggestedMinHeight =>
        CurrentArea == ShellArea.Focus ? FocusCompactMinHeight : PlanningMinHeight;

    public double SuggestedWidth =>
        CurrentArea == ShellArea.Focus ? FocusDefaultWidth : PlanningMinWidth;

    public double SuggestedHeight =>
        CurrentArea == ShellArea.Focus ? FocusDefaultHeight : PlanningMinHeight;

    public void NavigateTo(ShellArea area)
    {
        if (area != ShellArea.Focus && Focus.IsCompact)
        {
            Focus.ExitCompactMode();
        }

        if (area == ShellArea.Tasks)
        {
            Tasks.RefreshCommand.Execute(null);
        }
        else if (area == ShellArea.Projects)
        {
            Projects.RefreshCommand.Execute(null);
        }
        else if (area == ShellArea.Analytics)
        {
            Analytics.Refresh();
        }
        else if (area == ShellArea.Focus)
        {
            Focus.RefreshTaskLists();
        }

        CurrentArea = area;
    }

    public void NavigateToProject(Guid projectId)
    {
        Projects.SelectProject(projectId);
        NavigateTo(ShellArea.Projects);
    }

    private void OnFocusCompactModeChanged(object? sender, EventArgs e)
    {
        UpdateShowNavigation();
        RaiseWindowSizeHint();
    }

    private void UpdateShowNavigation()
    {
        ShowNavigation = !(CurrentArea == ShellArea.Focus && Focus.IsCompact);
    }

    private void RaiseWindowSizeHint()
    {
        OnPropertyChanged(nameof(SuggestedMinWidth));
        OnPropertyChanged(nameof(SuggestedMinHeight));
        OnPropertyChanged(nameof(SuggestedWidth));
        OnPropertyChanged(nameof(SuggestedHeight));
        WindowSizeHintChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Focus.CompactModeChanged -= OnFocusCompactModeChanged;
        Focus.Dispose();
    }
}
