namespace Jetset.App.Helpers;

public static class EffortFormatter
{
    public static string FormatHours(TimeSpan duration)
    {
        var hours = (int)Math.Round(duration.TotalHours, MidpointRounding.AwayFromZero);
        return $"{hours}h";
    }

    public static string FormatHours(int minutes) =>
        FormatHours(TimeSpan.FromMinutes(minutes));

    public static string FormatSpentEstimate(TimeSpan spent, int? estimateMinutes)
    {
        var spentText = FormatHours(spent);
        return estimateMinutes is int minutes
            ? $"{spentText} / {FormatHours(minutes)}"
            : spentText;
    }

    public static bool TryParseHours(string? input, out int? estimateMinutes)
    {
        estimateMinutes = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var trimmed = input.Trim();
        if (trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (!int.TryParse(trimmed, out var hours) || hours < 0)
        {
            return false;
        }

        estimateMinutes = hours * 60;
        return true;
    }
}
