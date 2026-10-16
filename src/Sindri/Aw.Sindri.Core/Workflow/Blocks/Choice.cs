using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One entry of a choice list: the outcome it names and the criterion that picks it (DT spec §3.2).</summary>
/// <param name="Outcome">The text before the first ": ".</param>
/// <param name="Criterion">The rest, 1 to 400 characters.</param>
public sealed record Choice(OutcomeName Outcome, string Criterion);
