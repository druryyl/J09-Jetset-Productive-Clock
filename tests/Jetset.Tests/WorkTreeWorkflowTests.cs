using System.IO;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

/// <summary>
/// End-to-end workflow validation for ADR-0007 success criteria walkthrough.
/// </summary>
public class WorkTreeWorkflowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static WorkTreeWorkflowTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public WorkTreeWorkflowTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetWorkflowTests", Guid.NewGuid().ToString("N"));
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
                // Best-effort cleanup.
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
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void CaptureOrganizeConvertStartSwitchComplete_RollupCorrect()
    {
        var services = CreateServices();
        using var vm = new WorkTreeViewModel(services);

        vm.QuickCaptureTitle = "Organize me";
        vm.QuickCaptureCommand.Execute(null);
        var captured = services.Tasks.ListByStatuses([TaskStatus.Inbox]).Single();
        Assert.Null(captured.ProjectId);

        var project = services.Projects.Create("Delivery");
        Assert.True(vm.TryReparentTask(captured.Id, project.Id, out _));
        Assert.Equal(project.Id, services.Tasks.Get(captured.Id)!.ProjectId);

        var growTask = services.Tasks.Create("Split feature");
        services.Tasks.ChangeStatus(growTask.Id, TaskStatus.Ready);
        var splitProject = services.WorkItemConversion.ConvertTaskToProject(growTask.Id);
        Assert.NotEqual(growTask.Id, splitProject.Id);
        Assert.DoesNotContain(services.Tasks.List(), t => t.Id == growTask.Id);

        var first = services.Tasks.Create("First task", project.Id);
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        services.Tasks.UpdateEstimate(first.Id, 120);

        var second = services.Tasks.Create("Second task", project.Id);
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        services.WorkExecution.StartWork(first.Id);
        services.WorkExecution.StartWork(second.Id, leavingStatus: TaskStatus.Waiting);
        Assert.Equal(TaskStatus.Waiting, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);

        services.WorkExecution.FinishWork();
        services.Tasks.CompleteTask(second.Id);
        Assert.Equal(TaskStatus.Done, services.Tasks.Get(second.Id)!.Status);
        Assert.Null(services.Tasks.GetRunningTask());

        vm.RefreshCommand.Execute(null);
        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project && n.Id == project.Id);
        Assert.Equal("0h / 2h", projectNode.EffortDisplayText);
    }

    [Fact]
    public void CompactOverlay_StartWorkPanel_DoesNotExitOverlay()
    {
        var services = CreateServices();
        using var shell = new ShellViewModel(services);

        shell.EnterCompactOverlay();
        Assert.True(shell.IsCompactOverlay);

        shell.Focus.StartWorkCommand.Execute(null);
        Assert.True(shell.IsCompactOverlay);
        Assert.True(shell.Focus.ShowStartPanel);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
