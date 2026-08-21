using Jetset.App.Models;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class ContextCaptureViewModelTests
{
    private static WorkTask CreateTask() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Review PR",
        Status = TaskStatus.Active,
        CurrentStatus = "In review",
        LastProgress = "Left comments",
        NextAction = "Wait for author",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void PauseReason_IsSkippableWithoutCancel()
    {
        var vm = new ContextCaptureViewModel(new ContextCaptureRequest
        {
            Task = CreateTask(),
            Reason = ContextCaptureReason.Pause
        });

        Assert.Equal("Pause work", vm.WindowTitle);
        Assert.True(vm.SkipIsCancel);
        Assert.False(vm.ShowCancel);
        Assert.False(vm.ShowSessionNote);
        Assert.Equal("In review", vm.CurrentStatus);
        Assert.Equal("Left comments", vm.LastProgress);
    }

    [Fact]
    public void FinishReason_ShowsLastProgressAndSessionNote()
    {
        var vm = new ContextCaptureViewModel(new ContextCaptureRequest
        {
            Task = CreateTask(),
            Reason = ContextCaptureReason.Finish
        });

        Assert.Equal("Finish session", vm.WindowTitle);
        Assert.True(vm.ShowCancel);
        Assert.True(vm.ShowSessionNote);
        Assert.False(vm.SkipIsCancel);
        Assert.Equal("What did you complete?", vm.PromptTitle);
    }

    [Fact]
    public void ToWorkingContext_UsesEditedFields()
    {
        var vm = new ContextCaptureViewModel(new ContextCaptureRequest
        {
            Task = CreateTask(),
            Reason = ContextCaptureReason.Switch
        })
        {
            LastProgress = "Merged",
            NextAction = "Deploy"
        };

        var context = vm.ToWorkingContext();
        Assert.Equal("Merged", context.LastProgress);
        Assert.Equal("Deploy", context.NextAction);
        Assert.Equal("In review", context.CurrentStatus);
    }
}
