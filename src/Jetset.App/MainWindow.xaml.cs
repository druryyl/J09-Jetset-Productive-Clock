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
    private readonly ShellViewModel _shellViewModel;
    private bool _forceClose;
    private bool _applyingSizeHint;

    public MainWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();

        _shellViewModel = new ShellViewModel(services);
        DataContext = _shellViewModel;

        _shellViewModel.Focus.OpenHistoryRequested += (_, _) =>
        {
            var window = new HistoryWindow(services) { Owner = this };
            window.ShowDialog();
        };

        _shellViewModel.Focus.OpenSettingsRequested += (_, _) =>
        {
            var window = new SettingsWindow(services) { Owner = this };
            window.ShowDialog();
            ApplyTheme();
        };

        _shellViewModel.Focus.RecoveryNeeded += (_, session) =>
        {
            var dialog = new RecoveryDialog(session) { Owner = this };
            dialog.ShowDialog();
            switch (dialog.Result)
            {
                case RecoveryResult.Continue:
                    _shellViewModel.Focus.ApplyRecoveryContinue();
                    break;
                case RecoveryResult.FinishLastKnown:
                    _shellViewModel.Focus.ApplyRecoveryFinishLastKnown();
                    break;
                case RecoveryResult.Discard:
                    _shellViewModel.Focus.ApplyRecoveryDiscard();
                    break;
            }
        };

        _shellViewModel.WindowSizeHintChanged += (_, _) => ApplyWindowSizeHint();

        ApplyWindowBounds();
        ApplyWindowSizeHint();
        ApplyTheme();

        Loaded += (_, _) =>
        {
            _shellViewModel.Focus.CheckRecovery();
            ShowV2WelcomeIfNeeded();
        };
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

    private void ShowV2WelcomeIfNeeded()
    {
        if (_services.Settings.Settings.HasSeenV2Welcome)
        {
            return;
        }

        var dialog = new V2WelcomeDialog(_services.Settings.Settings.UpgradedFromV1) { Owner = this };
        dialog.ShowDialog();

        _services.Settings.Update(settings =>
        {
            settings.HasSeenV2Welcome = true;
            settings.UpgradedFromV1 = false;
        });
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistBounds();
        _services.Settings.Save(_services.Settings.Settings);

        if (_forceClose || _services.Tray.ExitRequestedFlag)
        {
            _shellViewModel.Dispose();
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

    private void ApplyWindowSizeHint()
    {
        if (!IsLoaded && ActualWidth <= 0)
        {
            MinWidth = _shellViewModel.SuggestedMinWidth;
            MinHeight = _shellViewModel.SuggestedMinHeight;
            return;
        }

        _applyingSizeHint = true;
        try
        {
            MinWidth = _shellViewModel.SuggestedMinWidth;
            MinHeight = _shellViewModel.SuggestedMinHeight;

            if (Width < MinWidth)
            {
                Width = Math.Max(_shellViewModel.SuggestedWidth, MinWidth);
            }

            if (Height < MinHeight)
            {
                Height = Math.Max(_shellViewModel.SuggestedHeight, MinHeight);
            }
        }
        finally
        {
            _applyingSizeHint = false;
        }
    }

    private void PersistBounds()
    {
        if (!IsLoaded || WindowState != WindowState.Normal || _applyingSizeHint)
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
        var focus = _shellViewModel.Focus;

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
        {
            if (_shellViewModel.CurrentArea != ShellArea.Focus)
            {
                _shellViewModel.NavigateTo(ShellArea.Focus);
            }

            focus.RequestQuickCaptureFocus();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    if (_shellViewModel.CurrentArea != ShellArea.Focus)
                    {
                        _shellViewModel.NavigateTo(ShellArea.Focus);
                    }

                    if (focus.StartWorkCommand.CanExecute(null))
                    {
                        focus.StartWorkCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.P:
                    if (_shellViewModel.CurrentArea != ShellArea.Focus)
                    {
                        _shellViewModel.NavigateTo(ShellArea.Focus);
                    }

                    if (focus.IsRunning)
                    {
                        focus.PauseCommand.Execute(null);
                    }
                    else if (focus.IsPaused)
                    {
                        focus.ResumeCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (focus.FinishCommand.CanExecute(null))
                    {
                        if (_shellViewModel.CurrentArea != ShellArea.Focus)
                        {
                            _shellViewModel.NavigateTo(ShellArea.Focus);
                        }

                        focus.FinishCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.M:
                    if (_shellViewModel.CurrentArea != ShellArea.Focus)
                    {
                        _shellViewModel.NavigateTo(ShellArea.Focus);
                    }

                    focus.ToggleCompactCommand.Execute(null);
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
