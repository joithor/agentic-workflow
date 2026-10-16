using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A fact a block writes (spec §2).</summary>
/// <param name="Fact">The fact.</param>
/// <param name="RequiredOn">The outcomes it must be written on; empty means it is optional on every outcome unless <paramref name="Every"/> is true.</param>
/// <param name="Every">True when the fact is required on every outcome the Step can report (DT spec §8.1), including the ones its parameters declare. Written only when true, so a descriptor without it hashes as before; never false.</param>
/// <param name="EngineFilled">True when the engine writes the fact from the report when it is omitted (DT spec §8.1: "fact:decision" of the choice Steps), so a desk report never has to give it. Written only when true.</param>
public sealed record OutputSpec(
    FactKey Fact,
    IReadOnlyList<OutcomeName> RequiredOn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Every = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? EngineFilled = null);
