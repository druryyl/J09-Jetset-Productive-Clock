using Jetset.App.Models;

namespace Jetset.App.Services;

public sealed class ClockService
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
