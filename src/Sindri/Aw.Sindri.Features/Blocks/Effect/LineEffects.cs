using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The line's report effect (line spec BD7, LN-WF1): descriptor only until the line's dispatcher gives it a runtime. A recipe that
/// names it validates and evals; a served instance's dispatch of it is refused as an effect with no runtime.
/// </summary>
internal static class LineEffects
{
    /// <summary>'effect.line.report': tells the line a phase has 'shipped' or 'merged'; 'line' is the line context fact.</summary>
    public static BlockDescriptor Report { get; } = new(
        BlockId.From("effect.line.report"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects,
        Parameters:
        [
            new ParameterSpec("status", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("line", ParameterType.Text, Required: true, IsCapabilityField: false),
        ],
        Rung: null,
        DeskCapable: false);
}
