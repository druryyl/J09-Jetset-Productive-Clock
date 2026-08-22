using System.IO;
using System.Reflection;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class AnalyticsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static AnalyticsViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public AnalyticsViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetAnalyticsVmTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var path in _dbPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup of temp DB files.
            }
        }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of temp directory.
        }
    }

    [Fact]
    public void ViewModel_HasNoMomentumOrSwitchProperties()
    {
        var vm = new AnalyticsViewModel(CreateServices());

        Assert.Null(vm.GetType().GetProperty("MomentumWeeks", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("MomentumRangeText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("MomentumSummaryText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("SelectedProjectId", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("Projects", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("DailySwitchCounts", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("SwitchRangeText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("SwitchTotalText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("SwitchAverageText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("BusiestHourText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("HasMomentumSelection", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("HasMomentumWeeks", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("HasProjects", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ViewModel_ExposesPersonalMetricsProperties()
    {
        var vm = new AnalyticsViewModel(CreateServices());

        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.CurrentStreakText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.LongestStreakText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.HeatmapWeeks)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.HeatmapRangeText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.TotalFocusTimeText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.SessionSummaryText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.TaskBreakdown)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(AnalyticsViewModel.DailyFocusDateText)));
    }

    [Fact]
    public void Refresh_WithSessions_ShowsDailyFocusAndTaskBreakdown()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Review PR");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);
        services.Sessions.Finish();

        var vm = new AnalyticsViewModel(services);

        Assert.Contains("1 session", vm.SessionSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.HasTaskBreakdown);
        Assert.Single(vm.TaskBreakdown);
        Assert.Equal("Review PR", vm.TaskBreakdown[0].TaskTitle);
        Assert.Equal(1, vm.TaskBreakdown[0].SessionCount);
    }

    [Fact]
    public void Refresh_WithFocusTime_ShowsStreak()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Ship feature");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);
        services.Sessions.Finish();

        var vm = new AnalyticsViewModel(services);

        Assert.Equal("1 day", vm.CurrentStreakText);
        Assert.Equal("Best: 1 day", vm.LongestStreakText);
    }

    [Fact]
    public void Refresh_BuildsTwelveWeekHeatmap()
    {
        var vm = new AnalyticsViewModel(CreateServices());

        Assert.Equal(12, vm.HeatmapWeeks.Count);
        Assert.All(vm.HeatmapWeeks, week => Assert.Equal(7, week.Days.Count));
        Assert.False(string.IsNullOrWhiteSpace(vm.HeatmapRangeText));
    }

    [Fact]
    public void SelectedDate_UpdatesDailyFocusDateText()
    {
        var vm = new AnalyticsViewModel(CreateServices());
        var selected = new DateTime(2026, 8, 22);

        vm.SelectedDate = selected;

        Assert.Contains("Aug 22, 2026", vm.DailyFocusDateText, StringComparison.Ordinal);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
