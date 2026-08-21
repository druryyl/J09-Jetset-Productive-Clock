using System.Windows;
using System.Windows.Input;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using Jetset.App.Views;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Jetset.App;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly MainWindowViewModel _viewModel;
    private bool _forceClose;

    public MainWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();

        _viewModel = new MainWindowViewModel(services);
        DataContext = _viewModel;

        _viewModel.OpenHistoryRequested += (_, _) =>
        {
            var window = new HistoryWindow(services) { Owner = this };
            window.ShowDialog();
            // Refresh totals after edits
        };

        _viewModel.OpenTasksRequested += (_, _) =>
        {
            var window = new TasksWindow(services) { Owner = this };
            window.ShowDialog();
        };

        _viewModel.OpenSettingsRequested += (_, _) =>
        {
            var window = new SettingsWindow(services) { Owner = this };
            window.ShowDialog();
            ApplyTheme();
        };

        _viewModel.FinishNoteRequested += (_, _) =>
        {
            var dialog = new FinishNoteDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.CompleteFinish(dialog.Note);
            }
        };

        _viewModel.RecoveryNeeded += (_, session) =>
        {
            var dialog = new RecoveryDialog(session) { Owner = this };
            dialog.ShowDialog();
            switch (dialog.Result)
            {
                case RecoveryResult.Continue:
                    _viewModel.ApplyRecoveryContinue();
                    break;
                case RecoveryResult.FinishLastKnown:
                    _viewModel.ApplyRecoveryFinishLastKnown();
                    break;
                case RecoveryResult.Discard:
                    _viewModel.ApplyRecoveryDiscard();
                    break;
            }
        };

        ApplyWindowBounds();
        ApplyTheme();

        Loaded += (_, _) => _viewModel.CheckRecovery();
        LocationChanged += (_, _) => PersistBounds();
        SizeChanged += (_, _) => PersistBounds();
        Closing += OnClosing;
    }

    public void RequestExit()
    {
        _forceClose = true;
        Close();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistBounds();
        _services.Settings.Save(_services.Settings.Settings);

        if (_forceClose || _services.Tray.ExitRequestedFlag)
        {
            _viewModel.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ApplyWindowBounds()
    {
        var s = _services.Settings.Settings;
        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            Left = s.WindowLeft;
            Top = s.WindowTop;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (s.WindowWidth >= MinWidth)
        {
            Width = s.WindowWidth;
        }

        if (s.WindowHeight >= MinHeight)
        {
            Height = s.WindowHeight;
        }
    }

    private void PersistBounds()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        var s = _services.Settings.Settings;
        s.WindowLeft = Left;
        s.WindowTop = Top;
        s.WindowWidth = Width;
        s.WindowHeight = Height;
    }

    private void ApplyTheme()
    {
        ThemeManager.Apply(_services.Settings.Settings.UseDarkTheme);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    if (_viewModel.StartWorkCommand.CanExecute(null))
                    {
                        _viewModel.StartWorkCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.P:
                    if (_viewModel.IsRunning)
                    {
                        _viewModel.PauseCommand.Execute(null);
                    }
                    else if (_viewModel.IsPaused)
                    {
                        _viewModel.ResumeCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_viewModel.FinishCommand.CanExecute(null))
                    {
                        _viewModel.FinishCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.M:
                    _viewModel.ToggleCompactCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.H:
                    if (IsVisible)
                    {
                        Hide();
                    }
                    else
                    {
                        ShowFromTray();
                    }

                    e.Handled = true;
                    break;
            }
        }
    }
}
