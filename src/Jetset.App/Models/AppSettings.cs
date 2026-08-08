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

    public double WindowLeft { get; set; } = double.NaN;

    public double WindowTop { get; set; } = double.NaN;

    public double WindowWidth { get; set; } = 360;

    public double WindowHeight { get; set; } = 420;
}
