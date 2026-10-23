using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The line's report effect (line spec BD7, LN-WF1): its runtime is LineReportEffect: it records the report in the phase's run area and replies
/// 'line.phaseChanged' with 'fact:phaseEvent' for the line. The phase recipe sends 'shipped', 'merged' and 'failed'; the engine sends 'parked',
/// 'resumed' and 'ended' when a linked instance changes (<c language="text">LinkedPhaseReports</c>).
/// </summary>
internal static class LineEffects
{
    /// <summary>'effect.line.report': tells the line a phase has 'shipped', 'merged', 'failed', 'parked', 'resumed' or 'ended'; 'line' is the line context fact. 'parkReason' goes with 'parked'; 'ended' (the terminal outcome) and 'packet' (the phase packet's hash) with 'ended'. The engine also passes 'phasePacket' (the stored packet), whose 'phase' is the plan's id.</summary>
    public static BlockDescriptor Report { get; } = new(
        BlockId.From("effect.line.report"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [new OutputSpec(FactKey.From("fact:phaseEvent"), [])], Outcomes: [],
        Capability.Effects,
        Parameters:
        [
            new ParameterSpec("status", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("line", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("parkReason", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("ended", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("packet", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("phasePacket", ParameterType.Text, Required: false, IsCapabilityField: false),
        ],
        Rung: null,
        DeskCapable: false);
}
