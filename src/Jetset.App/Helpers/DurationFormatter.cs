using System.Globalization;

namespace Jetset.App.Helpers;

public static class DurationFormatter
{
    public static string FormatClock(DateTimeOffset now, bool use24Hour, bool showSeconds)
    {
        var format = use24Hour
            ? (showSeconds ? "HH:mm:ss" : "HH:mm")
            : (showSeconds ? "h:mm:ss tt" : "h:mm tt");

        return now.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
    }

    public static string FormatDate(DateTimeOffset now)
    {
        return now.ToLocalTime().ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
    }

    public static string FormatTimer(TimeSpan duration, bool allowNegativeSign = false)
    {
        var negative = duration < TimeSpan.Zero;
        var abs = duration.Duration();
        var text = abs.TotalHours >= 1
            ? $"{(int)abs.TotalHours:00}:{abs.Minutes:00}:{abs.Seconds:00}"
            : $"{abs.Minutes:00}:{abs.Seconds:00}";

        if (negative && allowNegativeSign)
        {
            return "-" + text;
        }

        return text;
    }

    public static string FormatFriendly(TimeSpan duration)
    {
        var abs = duration.Duration();
        var hours = (int)abs.TotalHours;
        var minutes = abs.Minutes;

        if (hours > 0 && minutes > 0)
        {
            return $"{hours}h {minutes}m";
        }

        if (hours > 0)
        {
            return $"{hours}h";
        }

        if (minutes > 0)
        {
            return $"{minutes}m";
        }

        return abs.TotalSeconds < 1 ? "0m" : $"{abs.Seconds}s";
    }

    public static string FormatTimeOfDay(DateTimeOffset value, bool use24Hour)
    {
        var format = use24Hour ? "HH:mm" : "h:mm tt";
        return value.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
    }

    public static string FormatOvertime(TimeSpan overtime)
    {
        return $"+{FormatTimer(overtime)} overtime";
    }
}
