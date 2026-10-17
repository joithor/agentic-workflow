using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLiveness on macOS: the process's age from "ps -o etime=" gives its start time (now minus the age), checked against when the file naming the pid was written.</summary>
/// <param name="reader">Runs ps.</param>
/// <param name="time">The clock the start time is computed from.</param>
internal sealed class MacProcessLiveness(MacHostReader reader, TimeProvider time) : IProcessLiveness
{
    public async ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        if (pid <= 0)
        {
            return false;
        }

        var now = time.GetUtcNow();   // before ps runs: a slow ps must not move the start time of a live process past the file's
        var age = await reader.ProcessAge(pid, cancellationToken).ConfigureAwait(false);
        if (age.IsError)
        {
            return age.Error;
        }

        return age.Value is { } running && !ProcessStart.IsReused(now - running, recordedAt);
    }
}
