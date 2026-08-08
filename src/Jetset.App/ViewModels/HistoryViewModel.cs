using System.Collections.ObjectModel;
using System.Globalization;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace Jetset.App.ViewModels;

public sealed class HistoryItemViewModel : ObservableObject
{
    private string _taskName;
    private string _note;

    public HistoryItemViewModel(WorkSession session, TimeSpan activeDuration, bool use24Hour)
    {
        Session = session;
        ActiveDuration = activeDuration;
        Use24Hour = use24Hour;
        _taskName = session.TaskName;
        _note = session.Note ?? string.Empty;
    }

    public WorkSession Session { get; }

    public TimeSpan ActiveDuration { get; private set; }

    public bool Use24Hour { get; }

    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string TimeRange
    {
        get
        {
            var start = DurationFormatter.FormatTimeOfDay(Session.StartedAt, Use24Hour);
            var end = Session.FinishedAt is { } finished
                ? DurationFormatter.FormatTimeOfDay(finished, Use24Hour)
                : "…";
            return $"{start}–{end}";
        }
    }

    public string DurationText => DurationFormatter.FormatFriendly(ActiveDuration);

    public string ModeText => Session.Mode == TimerMode.Countdown ? "Countdown" : "Stopwatch";

    public string StatusText => Session.State.ToString();

    public string SummaryLine => $"{TimeRange}   {TaskName}   {DurationText}";

    public void RefreshDuration(TimeSpan duration)
    {
        ActiveDuration = duration;
        OnPropertyChanged(nameof(ActiveDuration));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(SummaryLine));
        OnPropertyChanged(nameof(TimeRange));
        OnPropertyChanged(nameof(StatusText));
    }
}

public sealed class HistoryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private HistoryItemViewModel? _selected;
    private DateTime _selectedDate;
    private string _header = "TODAY";
    private string _editStart = string.Empty;
    private string _editFinish = string.Empty;
    private string _editDurationMinutes = string.Empty;
    private string? _message;

    public HistoryViewModel(AppServices services)
    {
        _services = services;
        _selectedDate = DateTime.Today;
        Items = new ObservableCollection<HistoryItemViewModel>();
        SaveEditCommand = new RelayCommand(SaveEdit, () => Selected is not null);
        RefreshCommand = new RelayCommand(Load);
        PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
        NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
        GoToTodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
        DeleteSessionCommand = new RelayCommand(DeleteSession, CanDeleteSelected);
        Load();
    }

    public ObservableCollection<HistoryItemViewModel> Items { get; }

    public RelayCommand SaveEditCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand PreviousDayCommand { get; }
    public RelayCommand NextDayCommand { get; }
    public RelayCommand GoToTodayCommand { get; }
    public RelayCommand DeleteSessionCommand { get; }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var date = value.Date;
            if (SetProperty(ref _selectedDate, date))
            {
                Load();
            }
        }
    }

    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    public HistoryItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                if (value is not null)
                {
                    EditStart = value.Session.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    EditFinish = value.Session.FinishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
                    EditDurationMinutes = ((int)Math.Round(value.ActiveDuration.TotalMinutes)).ToString();
                }

                SaveEditCommand.RaiseCanExecuteChanged();
                DeleteSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EditStart
    {
        get => _editStart;
        set => SetProperty(ref _editStart, value);
    }

    public string EditFinish
    {
        get => _editFinish;
        set => SetProperty(ref _editFinish, value);
    }

    public string EditDurationMinutes
    {
        get => _editDurationMinutes;
        set => SetProperty(ref _editDurationMinutes, value);
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool HasItems => Items.Count > 0;

    public void Load()
    {
        var previouslySelectedId = Selected?.Session.Id;
        Items.Clear();
        var use24 = _services.Settings.Settings.Use24HourClock;
        var day = new DateTimeOffset(SelectedDate);
        var sessions = _services.Sessions.GetTodaysSessions(day);
        foreach (var session in sessions)
        {
            if (session.State == SessionState.Cancelled)
            {
                continue;
            }

            var duration = _services.Sessions.GetActiveDuration(session.Id);
            Items.Add(new HistoryItemViewModel(session, duration, use24));
        }

        var total = _services.Sessions.GetTodaysTotal(day);
        var dateLabel = SelectedDate.ToString("ddd, d MMM yyyy", CultureInfo.CurrentCulture).ToUpperInvariant();
        Header = $"{dateLabel} — {DurationFormatter.FormatFriendly(total)}";
        OnPropertyChanged(nameof(HasItems));
        Message = Items.Count == 0 ? "No sessions recorded on this day." : null;

        Selected = previouslySelectedId is { } id
            ? Items.FirstOrDefault(i => i.Session.Id == id)
            : null;
    }

    private bool CanDeleteSelected()
    {
        if (Selected is null)
        {
            return false;
        }

        var state = Selected.Session.State;
        if (state is SessionState.Running or SessionState.Paused)
        {
            return false;
        }

        return _services.Sessions.ActiveSession?.Id != Selected.Session.Id;
    }

    private void DeleteSession()
    {
        if (Selected is null)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            $"Delete session \"{Selected.TaskName}\"? This cannot be undone.",
            "Delete session",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (result != WpfMessageBoxResult.Yes)
        {
            return;
        }

        Message = null;
        try
        {
            _services.Sessions.DeleteSession(Selected.Session.Id);
            Load();
            Message = "Session deleted.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void SaveEdit()
    {
        if (Selected is null)
        {
            return;
        }

        Message = null;
        try
        {
            if (!DateTime.TryParse(EditStart, out var startLocal))
            {
                Message = "Invalid start time.";
                return;
            }

            DateTimeOffset? finish = null;
            if (!string.IsNullOrWhiteSpace(EditFinish))
            {
                if (!DateTime.TryParse(EditFinish, out var finishLocal))
                {
                    Message = "Invalid finish time.";
                    return;
                }

                finish = new DateTimeOffset(finishLocal);
            }

            if (!int.TryParse(EditDurationMinutes, out var minutes) || minutes < 0)
            {
                Message = "Invalid duration (minutes).";
                return;
            }

            var session = Selected.Session;
            var taskName = Selected.TaskName.Trim();
            if (string.IsNullOrWhiteSpace(taskName))
            {
                Message = "Task name is required.";
                return;
            }

            var note = string.IsNullOrWhiteSpace(Selected.Note) ? null : Selected.Note.Trim();
            var startedAt = new DateTimeOffset(startLocal);
            var duration = TimeSpan.FromMinutes(minutes);
            var endedAt = startedAt.Add(duration);
            var isActive = session.State is SessionState.Running or SessionState.Paused;

            var updated = new WorkSession
            {
                Id = session.Id,
                TaskName = taskName,
                Mode = session.Mode,
                StartedAt = startedAt,
                FinishedAt = isActive ? null : (finish ?? endedAt),
                CountdownDuration = session.CountdownDuration,
                State = isActive ? session.State : SessionState.Completed,
                Note = note,
                LastHeartbeatAt = session.LastHeartbeatAt,
                CountdownEndsAt = session.CountdownEndsAt,
                CountdownRemaining = session.CountdownRemaining,
                CountdownCompletedNotified = session.CountdownCompletedNotified
            };

            var intervals = new List<WorkInterval>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    WorkSessionId = session.Id,
                    StartedAt = startedAt,
                    EndedAt = isActive ? null : (finish ?? endedAt)
                }
            };

            _services.Sessions.UpdateSessionDetails(updated, intervals);
            Load();
            Message = "Session updated.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
