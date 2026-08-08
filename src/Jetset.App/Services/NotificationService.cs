using System.Media;

namespace Jetset.App.Services;

public sealed class NotificationService
{
    public void ShowCountdownCompleted(string taskName, bool playSound)
    {
        if (playSound)
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Ignore sound failures.
            }
        }
    }

    public void ShowBalloon(string title, string text)
    {
        BalloonRequested?.Invoke(this, new BalloonEventArgs(title, text));
    }

    public event EventHandler<BalloonEventArgs>? BalloonRequested;
}

public sealed class BalloonEventArgs : EventArgs
{
    public BalloonEventArgs(string title, string text)
    {
        Title = title;
        Text = text;
    }

    public string Title { get; }

    public string Text { get; }
}
