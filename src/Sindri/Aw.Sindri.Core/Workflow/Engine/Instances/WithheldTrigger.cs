using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// The trigger a park or escalation withheld (spec §5.5 step 4, §5.8), stored on the suspended frame so its resume
/// can perform or re-evaluate it. Its own key is never replayed: the resume commits under the answer's key.
/// </summary>
/// <param name="Transition">The index of the matched transition in the recipe's transitions; null when the resume only re-runs the Step.</param>
/// <param name="Trigger">The withheld trigger, with its outcome and outputs (the findings).</param>
public sealed record WithheldTrigger(int? Transition, Trigger Trigger);
