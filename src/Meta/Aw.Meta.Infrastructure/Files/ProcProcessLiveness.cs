using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLiveness on Linux: the process's start time from /proc/&lt;pid&gt;/stat and /proc/uptime, checked against when the file naming the pid was written.</summary>
/// <param name="time">The clock the start time is computed from.</param>
internal sealed class ProcProcessLiveness(TimeProvider time) : IProcessLiveness
{
    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        _ = (time, pid, recordedAt, cancellationToken);
        throw new NotSupportedException("red: liveness by start time");
    }
}
