using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class ContextCaptureViewModel : ObservableObject
{
    public ContextCaptureViewModel(ContextCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Reason = request.Reason;
        TaskTitle = request.Task.Title;
        CurrentStatus = request.Task.CurrentStatus ?? string.Empty;
        LastProgress = request.Task.LastProgress ?? string.Empty;
        NextAction = request.Task.NextAction ?? string.Empty;
        Blocker = request.Task.Blocker ?? string.Empty;
        Notes = request.Task.Notes ?? string.Empty;
        SessionNote = string.Empty;
    }

    public ContextCaptureReason Reason { get; }

    public string TaskTitle { get; }

    public bool ShowSessionNote => Reason == ContextCaptureReason.Finish;

    public bool ShowCancel => Reason == ContextCaptureReason.Finish;

    public bool SkipIsCancel => !ShowCancel;

    public string WindowTitle => Reason switch
    {
        ContextCaptureReason.Finish => "Finish session",
        ContextCaptureReason.Switch => "Switch task",
        _ => "Pause work"
    };

    public string PromptTitle => Reason switch
    {
        ContextCaptureReason.Finish => "What did you complete?",
        ContextCaptureReason.Switch => "Preserve context before switching",
        _ => "Update context before pausing"
    };

    public string PromptMessage => Reason switch
    {
        ContextCaptureReason.Finish =>
            "Last progress is saved on the task. Skip to finish without editing.",
        ContextCaptureReason.Switch =>
            "The current task is paused after this. Skip to switch without editing.",
        _ =>
            "This is saved as a snapshot so you can resume quickly. Skip to pause without editing."
    };

    public string CurrentStatus { get; set; }

    public string LastProgress { get; set; }

    public string NextAction { get; set; }

    public string Blocker { get; set; }

    public string Notes { get; set; }

    public string SessionNote { get; set; }

    public WorkingContext ToWorkingContext() => new()
    {
        CurrentStatus = CurrentStatus,
        LastProgress = LastProgress,
        NextAction = NextAction,
        Blocker = Blocker,
        Notes = Notes
    };
}
