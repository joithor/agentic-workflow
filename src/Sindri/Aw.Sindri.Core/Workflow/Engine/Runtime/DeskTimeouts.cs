using System.Globalization;
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
    public static TimeSpan Wait(TimeoutSpec? timeout, TimeSpan floor) =>
        timeout is { After: var after } && after.Span > floor ? after.Span : floor;

    /// <summary>The instance after its desk wait of <paramref name="waited"/> expired: parked with 'timeout', the same run left open.</summary>
    /// <param name="instance">The instance, in an active state with an open desk run.</param>
    /// <param name="waited">How long it waited (<see cref="Wait"/>).</param>
    /// <param name="actor">The timer's actor ('service:workflow.timers').</param>
    /// <returns>The parked instance; the same instance when it is already suspended or ended.</returns>
    public static WorkflowInstance Expire(WorkflowInstance instance, TimeSpan waited, string actor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(actor);
        if (instance.Ended || instance.Suspension is not null)
        {
            return instance;
        }

        var park = new Suspension(
            SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null,
            $"{instance.Frame.State} ran past {Text(waited)}", actor, Mirrors: null,
            new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effect: null));
        return instance with { Suspension = park };
    }

    /// <summary>A duration as status writes it (spec §6.4): '2d', '2d 1h', '4h 30m', or '40s' under a minute.</summary>
    private static string Text(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds}s");
        }

        var parts = new List<string>(3);
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        if (span.Minutes > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        }

        return string.Join(' ', parts);
    }
}
