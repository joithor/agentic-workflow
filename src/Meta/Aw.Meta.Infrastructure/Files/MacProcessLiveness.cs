using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLiveness on macOS: the process's age from "ps -o etime=" gives its start time (now minus the age), checked against when the file naming the pid was written.</summary>
/// <param name="reader">Runs ps.</param>
/// <param name="time">The clock the start time is computed from.</param>
internal sealed class MacProcessLiveness(MacHostReader reader, TimeProvider time) : IProcessLiveness
{
    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        _ = (reader, time, pid, recordedAt, cancellationToken);
        throw new NotSupportedException("red: liveness by start time");
    }
}
