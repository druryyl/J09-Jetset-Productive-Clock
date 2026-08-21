namespace Jetset.App.Models;

public sealed class AppSettings
{
    public bool AlwaysOnTop { get; set; }

    public bool CompactMode { get; set; }

    public bool Use24HourClock { get; set; } = true;

    public bool ShowSeconds { get; set; } = true;

    public bool SoundOnCountdownComplete { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool UseDarkTheme { get; set; }

    public bool AutoPauseWhenIdle { get; set; }

    public int IdleTimeoutMinutes { get; set; } = 5;

    public bool AutoResumeAfterIdle { get; set; } = true;

    public double WindowLeft { get; set; } = double.NaN;

    public double WindowTop { get; set; } = double.NaN;

    public double WindowWidth { get; set; } = 360;

    public double WindowHeight { get; set; } = 420;

    public bool HasSeenV2Welcome { get; set; }

    public bool UpgradedFromV1 { get; set; }
}
