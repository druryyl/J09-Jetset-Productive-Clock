using System.Windows;
using Jetset.App.Services;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class TasksWindow : Window
{
    public TasksWindow(AppServices services)
    {
        InitializeComponent();
        DataContext = new TasksViewModel(services);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
