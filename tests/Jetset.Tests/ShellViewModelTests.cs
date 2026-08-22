using System.IO;
using System.Windows;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;

namespace Jetset.Tests;

public class ShellViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static ShellViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public ShellViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetShellVmTests", Guid.NewGuid().ToString("N"));
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
    public void NavigateToProject_SelectsProjectInWorkTree()
    {
        var services = CreateServices();
        using var shell = new ShellViewModel(services);

        var project = services.Projects.Create("Mobile app");
        services.Projects.Create("Other");

        shell.NavigateToProject(project.Id);

        Assert.Equal(ShellArea.WorkTree, shell.CurrentArea);
        Assert.NotNull(shell.WorkTree.SelectedNode);
        Assert.Equal(project.Id, shell.WorkTree.SelectedNode!.Id);
        Assert.Equal(WorkItemKind.Project, shell.WorkTree.SelectedNode.Kind);
    }

    [Fact]
    public void PrimaryNavigation_IsWorkTreeAndSettingsOnly()
    {
        var values = Enum.GetValues<ShellArea>();
        Assert.Equal(2, values.Length);
        Assert.Contains(ShellArea.WorkTree, values);
        Assert.Contains(ShellArea.Settings, values);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
