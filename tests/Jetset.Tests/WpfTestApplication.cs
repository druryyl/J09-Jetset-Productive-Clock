using System.Windows;

namespace Jetset.Tests;

internal static class WpfTestApplication
{
    private static readonly object InitLock = new();

    public static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (Application.Current is not null)
            {
                return;
            }

            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }
}
