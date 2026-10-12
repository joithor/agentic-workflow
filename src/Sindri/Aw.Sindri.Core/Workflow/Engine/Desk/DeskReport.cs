using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>A desk report (engine spec rev 8 §5.3, §6.3 'workflow complete'). Engine P0 spike.</summary>
/// <param name="Address">The instance.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">Each output fact and the file holding its content.</param>
/// <param name="Temporal">The Temporal frontend ('host:port'); null for the dev server's default, 127.0.0.1:7233.</param>
/// <param name="Wait">Whether to wait for the instance to leave the run ('--no-wait' doesn't: no worker may be running).</param>
public sealed record DeskReport(
    InstanceAddress Address,
    RunToken Run,
    OutcomeName Outcome,
    IReadOnlyDictionary<FactKey, string> Outputs,
    string? Temporal,
    bool Wait);
