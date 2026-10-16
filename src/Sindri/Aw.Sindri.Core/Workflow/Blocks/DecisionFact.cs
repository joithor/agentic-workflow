using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The content of an engine-filled 'fact:decision' (DT spec §8.1): the choice a Step reported and who reported it.</summary>
/// <param name="Choice">The reported outcome.</param>
/// <param name="Provider">"desk" for a human's report, "eval" for a recipe eval.</param>
/// <param name="Actor">Who reported; null in an eval, which has no actor.</param>
public sealed record DecisionFact(string Choice, string Provider, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Actor = null);
