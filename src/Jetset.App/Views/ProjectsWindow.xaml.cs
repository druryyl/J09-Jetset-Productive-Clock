using System.Windows;
using Jetset.App.Services;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class ProjectsWindow : Window
{
    public ProjectsWindow(AppServices services)
    {
        InitializeComponent();
        DataContext = new ProjectsViewModel(services);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
