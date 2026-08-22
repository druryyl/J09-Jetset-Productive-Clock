using Jetset.App.Helpers;

namespace Jetset.Tests;

public class EffortFormatterTests
{
    [Fact]
    public void FormatSpentEstimate_ShowsBothWhenEstimateSet()
    {
        var text = EffortFormatter.FormatSpentEstimate(TimeSpan.FromHours(18), 720);

        Assert.Equal("18h / 12h", text);
    }

    [Fact]
    public void FormatSpentEstimate_ShowsSpentOnlyWhenNoEstimate()
    {
        var text = EffortFormatter.FormatSpentEstimate(TimeSpan.FromHours(5), null);

        Assert.Equal("5h", text);
    }

    [Theory]
    [InlineData("12", 720)]
    [InlineData("12h", 720)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public void TryParseHours_ParsesValidInput(string input, int? expectedMinutes)
    {
        var success = EffortFormatter.TryParseHours(input, out var minutes);

        Assert.True(success);
        Assert.Equal(expectedMinutes, minutes);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    public void TryParseHours_RejectsInvalidInput(string input)
    {
        var success = EffortFormatter.TryParseHours(input, out _);

        Assert.False(success);
    }
}
