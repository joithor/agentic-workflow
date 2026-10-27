using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The calls 'session wrap' makes to the control plane (API spec §8.1), each proven by the launch's secret.</summary>
internal interface IWrapCalls
{
    /// <summary>Says the session is alive.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="session">The agent's session id once found, otherwise null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or why the control plane refused or was unreachable.</returns>
    public ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken);

    /// <summary>Says the agent exited.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="ended">The exit code, the agent's session id if found, and the limit that killed it if any.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or why the control plane refused or was unreachable.</returns>
    public ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, WrapExit ended, CancellationToken cancellationToken);

    /// <summary>Asks whether the launch's run is suspended (its wall clock is paused while it waits).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it is suspended, or why the control plane refused or was unreachable.</returns>
    public ValueTask<Result<bool>> Suspended(LaunchId launch, LaunchSecret proof, CancellationToken cancellationToken);
}
