using System.Reflection;
using Jetset.App.Services;

namespace Jetset.Tests;

public class RemovedDomainTypesTests
{
    private static readonly string[] RemovedTypes =
    [
        "Jetset.App.Models.Milestone",
        "Jetset.App.Models.MilestoneProgress",
        "Jetset.App.Models.ContextSnapshot",
        "Jetset.App.Models.WorkingContext",
        "Jetset.App.Models.ResumeQueueEntry",
        "Jetset.App.Models.TaskSwitchEvent",
        "Jetset.App.Services.MilestoneService",
        "Jetset.App.Services.ContextSnapshotService",
        "Jetset.App.Services.ResumeQueueService",
        "Jetset.App.Persistence.IMilestoneStore",
        "Jetset.App.Persistence.MilestoneStore",
        "Jetset.App.Persistence.InMemoryMilestoneStore",
        "Jetset.App.Persistence.IContextSnapshotStore",
        "Jetset.App.Persistence.ContextSnapshotStore",
        "Jetset.App.Persistence.InMemoryContextSnapshotStore",
        "Jetset.App.Persistence.ITaskSwitchEventStore",
        "Jetset.App.Persistence.TaskSwitchEventStore",
        "Jetset.App.Persistence.InMemoryTaskSwitchEventStore",
        "Jetset.App.ViewModels.MilestoneListItemViewModel",
        "Jetset.App.ViewModels.ContextSnapshotItemViewModel",
        "Jetset.App.ViewModels.ResumeQueueItemViewModel",
        "Jetset.App.ViewModels.ContextCaptureViewModel",
        "Jetset.App.ViewModels.ContextCaptureRequest",
        "Jetset.App.ViewModels.ProjectMomentumPresenter",
        "Jetset.App.ViewModels.ProjectMomentumWeekItemViewModel",
        "Jetset.App.Views.ContextCaptureDialog",
        "Jetset.App.Views.FocusView",
        "Jetset.App.Views.TasksView",
        "Jetset.App.Views.ProjectsView",
        "Jetset.App.Views.SettingsWindow",
    ];

    [Fact]
    public void Assembly_HasNoRemovedDomainTypes()
    {
        var assembly = typeof(AppServices).Assembly;

        foreach (var typeName in RemovedTypes)
        {
            Assert.Null(assembly.GetType(typeName));
        }
    }

    [Fact]
    public void AppServices_HasNoRemovedServiceProperties()
    {
        var properties = typeof(AppServices)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in new[] { "Milestones", "Snapshots", "ContextSnapshots", "ResumeQueue", "TaskSwitchEvents" })
        {
            Assert.DoesNotContain(name, properties);
        }
    }

    [Fact]
    public void AnalyticsService_HasNoRemovedMetricMethods()
    {
        var methods = typeof(AnalyticsService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("GetProjectMomentum", methods);
        Assert.DoesNotContain("GetSwitchMetrics", methods);
    }

    [Fact]
    public void TaskService_HasNoMilestoneMethods()
    {
        var methods = typeof(TaskService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("AssignToMilestone", methods);
        Assert.DoesNotContain("UnassignFromMilestone", methods);
    }
}
