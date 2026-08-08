using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Jetset.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotificationService _notifications;
    private NotifyIcon? _notifyIcon;
    private bool _exitRequested;

    public TrayService(NotificationService notifications)
    {
        _notifications = notifications;
        _notifications.BalloonRequested += OnBalloonRequested;
    }

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public bool ExitRequestedFlag => _exitRequested;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Jetset", null, (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _exitRequested = true;
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });

        _notifyIcon = new NotifyIcon
        {
            Text = "Jetset",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowBalloon(string title, string text)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void OnBalloonRequested(object? sender, BalloonEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() => ShowBalloon(e.Title, e.Text));
    }

    public void Dispose()
    {
        _notifications.BalloonRequested -= OnBalloonRequested;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
