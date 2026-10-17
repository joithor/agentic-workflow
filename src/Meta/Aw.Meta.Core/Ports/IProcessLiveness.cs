using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Whether a process id still belongs to the process a session or lock file was written for.</summary>
public interface IProcessLiveness
{
    /// <summary>
    /// Whether <paramref name="pid"/> is a running process that did not start after <paramref name="recordedAt"/>, the time the
    /// file naming it was last written (see <see cref="Health.ProcessStart"/>): a process that started later is another process
    /// that was given the pid after the first one exited.
    /// </summary>
    /// <param name="pid">The process id.</param>
    /// <param name="recordedAt">When the session or lock file that names the pid was last written.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when it is that process; false when there is none or the pid was reused; AW-META-010 when the host can't be asked.</returns>
    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken);
}
