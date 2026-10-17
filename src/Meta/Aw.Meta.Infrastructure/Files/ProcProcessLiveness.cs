using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessLiveness on Linux: the process's start time from /proc/&lt;pid&gt;/stat (clock ticks since boot, 100 a second) and
/// /proc/uptime, checked against when the file naming the pid was written. A /proc entry that vanishes mid-read is a process that exited.
/// </summary>
/// <param name="time">The clock the start time is computed from.</param>
internal sealed class ProcProcessLiveness(TimeProvider time) : IProcessLiveness
{
    private const double ClockTicksPerSecond = 100;

    public async ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            var stat = await File.ReadAllTextAsync("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat", cancellationToken).ConfigureAwait(false);
            if (ProcFormat.Stat(stat) is not { } fields)
            {
                return false;
            }

            var uptime = double.Parse((await File.ReadAllTextAsync("/proc/uptime", cancellationToken).ConfigureAwait(false)).Split(' ')[0], CultureInfo.InvariantCulture);
            var age = TimeSpan.FromSeconds(Math.Max(0, uptime - (fields.StartTicks / ClockTicksPerSecond)));
            return !ProcessStart.IsReused(time.GetUtcNow() - age, recordedAt);
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or FormatException or OverflowException)
        {
            return MetaErrors.HostUnreadable.With("/proc could not be read: " + exception.Message);
        }
    }
}
