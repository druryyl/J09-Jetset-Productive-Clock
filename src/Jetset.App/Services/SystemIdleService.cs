using System.Runtime.InteropServices;

namespace Jetset.App.Services;

public sealed class SystemIdleService : ISystemIdleService
{
    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo
        {
            CbSize = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var idleMs = unchecked(Environment.TickCount - (int)info.DwTime);
        if (idleMs < 0)
        {
            idleMs = 0;
        }

        return TimeSpan.FromMilliseconds(idleMs);
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
