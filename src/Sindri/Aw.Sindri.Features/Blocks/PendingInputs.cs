using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The context lane's Step inputs (scoping spec §9, requests C1, C3, C5 and W6), shipped as descriptors so the scoping
/// recipe validates. At run time an input without a runtime is unavailable: the lane that owns the input replaces its
/// descriptor here with the block and keeps the id, the parameters and the hash-relevant fields it declares.
/// Delete each entry when the context lane registers the real block (same id and params).
/// </summary>
internal static class PendingInputs
{
    /// <summary>'input.context.code': the files, tests, data, callers and callees for the anchors (C3).</summary>
    public static BlockDescriptor Code { get; } = Input("input.context.code", Anchors);

    private static ParameterSpec Anchors => new("anchors", ParameterType.Text, Required: true, IsCapabilityField: false);

    private static BlockDescriptor Input(string id, params ParameterSpec[] parameters) => new(
        BlockId.From(id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools, parameters, Rung: null, DeskCapable: false);
}
