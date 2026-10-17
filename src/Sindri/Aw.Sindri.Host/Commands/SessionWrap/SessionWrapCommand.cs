
namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// 'sindri session wrap --launch &lt;id&gt; -- &lt;agent cli …&gt;' (API spec §8.1, launcher spec §3, §4): runs the agent as a child, sets its Sindri environment, checks the
/// CLI is there (exit 127, SND-LAUNCH-003), sends a heartbeat every 30 s, waits up to 30 s for the agent's own session id and sends it on the next heartbeat,
/// and reports the exit. It exits with the agent's code.
/// </summary>
/// <param name="calls">The control plane calls.</param>
/// <param name="children">Starts the agent.</param>
/// <param name="environment">Wrap's environment.</param>
/// <param name="clock">The time the heartbeat and the id wait run on.</param>
/// <param name="errors">Where wrap writes its own messages (never the agent's output).</param>
/// <param name="ids">Where the session id is looked for.</param>
internal sealed class SessionWrapCommand(IWrapCalls calls, IChildLauncher children, Func<string, string?> environment, TimeProvider clock, TextWriter errors, ISessionIdSource ids)
{
    internal Task<int> Run(string[] args, CancellationToken cancellationToken)
    {
        _ = (calls, children, environment, clock, errors, ids, args, cancellationToken);
        throw new NotSupportedException("SessionWrapCommand: make SessionWrapTests pass");
    }
}
