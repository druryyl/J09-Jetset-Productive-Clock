using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class InProgressSessionItem : ObservableObject
{
    private string _durationText = string.Empty;
    private string _statusText = "Waiting";

    public InProgressSessionItem(WorkSession session, RelayCommand switchCommand)
    {
        SessionId = session.Id;
        TaskName = session.TaskName;
        SwitchCommand = switchCommand;
    }

    public Guid SessionId { get; }

    public string TaskName { get; }

    public RelayCommand SwitchCommand { get; }

    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
