using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public enum ContextCaptureReason
{
    Pause,
    Switch,
    Finish
}

public enum ContextCaptureResult
{
    Skipped,
    Saved,
    Cancelled
}

public sealed class ContextCaptureRequest
{
    public required WorkTask Task { get; init; }

    public required ContextCaptureReason Reason { get; init; }

    public ContextCaptureResult Result { get; set; } = ContextCaptureResult.Skipped;

    public WorkingContext? Context { get; set; }

    public string? SessionNote { get; set; }
}
