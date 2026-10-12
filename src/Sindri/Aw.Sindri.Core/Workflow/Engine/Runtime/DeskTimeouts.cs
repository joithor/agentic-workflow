using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The desk runtime's timeout (engine spec W20, §3.4, §3.8): a desk Step's wait is at least the profile's
/// 'workflow.deskTimeout' (default 2d), and its expiry always parks with 'timeout'; 'then: retry' and 'then: escalate'
/// are replaced by 'park' in desk mode. Pure, so the interpreter and its unit tests call the same code. Engine P4 unit
/// (c); the agent runtime's timers and 'then: retry' are P5's.
/// </summary>
public static class DeskTimeouts
{
    /// <summary>The 'workflow.deskTimeout' default (W20), until S1-D11's profile key sets it.</summary>
    public static TimeSpan DefaultFloor { get; } = TimeSpan.FromDays(2);

    /// <summary>How long a desk Step waits for its report: the state's timeout, raised to <paramref name="floor"/>.</summary>
    /// <param name="timeout">The state's timeout; an active state always has one (§3.4).</param>
    /// <param name="floor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The wait.</returns>
    public static TimeSpan Wait(TimeoutSpec? timeout, TimeSpan floor) => throw new NotSupportedException("red: engine P4 unit (c)");

    /// <summary>The instance after its desk wait of <paramref name="waited"/> expired: parked with 'timeout', the same run left open.</summary>
    /// <param name="instance">The instance, in an active state with an open desk run.</param>
    /// <param name="waited">How long it waited (<see cref="Wait"/>).</param>
    /// <param name="actor">The timer's actor ('service:workflow.timers').</param>
    /// <returns>The parked instance; the same instance when it is already suspended or ended.</returns>
    public static WorkflowInstance Expire(WorkflowInstance instance, TimeSpan waited, string actor) => throw new NotSupportedException("red: engine P4 unit (c)");
}
