namespace Jetset.App.ViewModels;

public sealed class FocusTaskPickerItemViewModel
{
    public FocusTaskPickerItemViewModel(Guid id, string title, string statusText, string? projectName)
    {
        Id = id;
        Title = title;
        StatusText = statusText;
        ProjectName = projectName;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string StatusText { get; }

    public string? ProjectName { get; }

    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectName);

    public string ProjectDisplay => ProjectName ?? string.Empty;
}
