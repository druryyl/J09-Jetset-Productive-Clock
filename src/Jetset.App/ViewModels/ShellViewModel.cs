using Jetset.App.Helpers;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    public const double CompactOverlayMinWidth = 280;
    public const double CompactOverlayMinHeight = 160;
    public const double CompactOverlayWidth = 360;
    public const double CompactOverlayHeight = 480;
    public const double PlanningMinWidth = 720;
    public const double PlanningMinHeight = 560;

    private ShellArea _currentArea = ShellArea.WorkTree;

    public ShellViewModel(AppServices services)
    {
        WorkTree = new WorkTreeViewModel(services);
        Settings = new SettingsAreaViewModel(services);
        Focus = new FocusViewModel(services);
        Search = new GlobalSearchViewModel(services);

        NavigateWorkTreeCommand = new RelayCommand(() => NavigateTo(ShellArea.WorkTree));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo(ShellArea.Settings));
        ToggleCompactOverlayCommand = new RelayCommand(ToggleCompactOverlay);

        Focus.CompactModeChanged += OnCompactOverlayChanged;
        Search.WorkStarted += (_, _) => NavigateTo(ShellArea.WorkTree);
        RaiseWindowSizeHint();
    }

    public WorkTreeViewModel WorkTree { get; }

    public SettingsAreaViewModel Settings { get; }

    public FocusViewModel Focus { get; }

    public GlobalSearchViewModel Search { get; }

    public RelayCommand NavigateWorkTreeCommand { get; }

    public RelayCommand NavigateSettingsCommand { get; }

    public RelayCommand ToggleCompactOverlayCommand { get; }

    public event EventHandler? WindowSizeHintChanged;

    public ShellArea CurrentArea
    {
        get => _currentArea;
        private set
        {
            if (SetProperty(ref _currentArea, value))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
                OnPropertyChanged(nameof(IsWorkTreeSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                RaiseWindowSizeHint();
            }
        }
    }

    public object CurrentViewModel => CurrentArea switch
    {
        ShellArea.Settings => Settings,
        _ => WorkTree
    };

    public bool ShowNavigation => !IsCompactOverlay;

    public bool IsCompactOverlay => Focus.IsCompact;

    public bool IsWorkTreeSelected => CurrentArea == ShellArea.WorkTree;

    public bool IsSettingsSelected => CurrentArea == ShellArea.Settings;

    public double SuggestedMinWidth => IsCompactOverlay ? CompactOverlayMinWidth : PlanningMinWidth;

    public double SuggestedMinHeight => IsCompactOverlay ? CompactOverlayMinHeight : PlanningMinHeight;

    public double SuggestedWidth => IsCompactOverlay ? CompactOverlayWidth : PlanningMinWidth;

    public double SuggestedHeight => IsCompactOverlay ? CompactOverlayHeight : PlanningMinHeight;

    public void NavigateTo(ShellArea area)
    {
        if (area == ShellArea.WorkTree)
        {
            WorkTree.RefreshCommand.Execute(null);
        }
        else if (area == ShellArea.Settings)
        {
            Settings.Analytics.Refresh();
        }

        CurrentArea = area;
    }

    public void NavigateToProject(Guid projectId)
    {
        NavigateTo(ShellArea.WorkTree);
        WorkTree.SelectProject(projectId);
    }

    public void EnterCompactOverlay()
    {
        if (!Focus.IsCompact)
        {
            Focus.IsCompact = true;
        }
    }

    private void ToggleCompactOverlay()
    {
        if (Focus.IsCompact)
        {
            Focus.ExitCompactMode();
        }
        else
        {
            EnterCompactOverlay();
        }
    }

    private void OnCompactOverlayChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsCompactOverlay));
        OnPropertyChanged(nameof(ShowNavigation));
        RaiseWindowSizeHint();
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
        Focus.CompactModeChanged -= OnCompactOverlayChanged;
        Focus.Dispose();
        WorkTree.Dispose();
    }
}
