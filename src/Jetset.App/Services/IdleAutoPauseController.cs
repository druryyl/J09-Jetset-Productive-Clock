using Jetset.App.Models;

namespace Jetset.App.Services;

/// <summary>
/// Decides when to auto-pause / auto-resume based on system idle, lock, and sleep.
/// Call <see cref="Evaluate"/> on a short timer; forward lock/power events separately.
/// </summary>
public sealed class IdleAutoPauseController
{
    public static readonly TimeSpan ActivityThreshold = TimeSpan.FromSeconds(2);

    private readonly SessionService _sessions;
    private readonly WorkExecutionService _execution;
    private readonly ISystemIdleService _idle;
    private readonly Func<AppSettings> _settings;

    public IdleAutoPauseController(
        SessionService sessions,
        WorkExecutionService execution,
        ISystemIdleService idle,
        Func<AppSettings> settings)
    {
        _sessions = sessions;
        _execution = execution;
        _idle = idle;
        _settings = settings;
    }

    public bool PausedByIdle { get; private set; }

    public event EventHandler? StateChanged;

    public void Evaluate()
    {
        var settings = _settings();
        if (!settings.AutoPauseWhenIdle)
        {
            return;
        }

        var session = _sessions.ActiveSession;
        if (session is null)
        {
            ClearIdlePauseFlag();
            return;
        }

        var idle = _idle.GetIdleTime();
        var timeout = TimeSpan.FromMinutes(Math.Clamp(settings.IdleTimeoutMinutes, 1, 60));

        if (session.State == SessionState.Running && idle >= timeout)
        {
            PauseDueToIdle();
            return;
        }

        if (settings.AutoResumeAfterIdle
            && PausedByIdle
            && session.State == SessionState.Paused
            && idle < ActivityThreshold)
        {
            ResumeFromIdle();
        }
    }

    public void OnSessionLockedOrSuspended()
    {
        if (!_settings().AutoPauseWhenIdle)
        {
            return;
        }

        var session = _sessions.ActiveSession;
        if (session?.State == SessionState.Running)
        {
            PauseDueToIdle();
        }
    }

    public void OnSessionUnlockedOrResumed()
    {
        var settings = _settings();
        if (!settings.AutoPauseWhenIdle || !settings.AutoResumeAfterIdle || !PausedByIdle)
        {
            return;
        }

        var session = _sessions.ActiveSession;
        if (session?.State == SessionState.Paused)
        {
            ResumeFromIdle();
        }
    }

    public void NotifyManualPause()
    {
        ClearIdlePauseFlag();
    }

    public void NotifyManualResume()
    {
        ClearIdlePauseFlag();
    }

    public void NotifySessionEnded()
    {
        ClearIdlePauseFlag();
    }

    private void PauseDueToIdle()
    {
        try
        {
            PausedByIdle = true;
            _execution.PauseWork();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException)
        {
            PausedByIdle = false;
            // Session may have changed between check and pause.
        }
    }

    private void ResumeFromIdle()
    {
        try
        {
            PausedByIdle = false;
            _sessions.Resume();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException)
        {
            // Session may have changed between check and resume.
            PausedByIdle = true;
        }
    }

    private void ClearIdlePauseFlag()
    {
        if (!PausedByIdle)
        {
            return;
        }

        PausedByIdle = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
