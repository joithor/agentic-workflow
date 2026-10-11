using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>Everything one decision reads (spec §5.3): the row, its recipe, the trigger, the gate answers and the context.</summary>
/// <param name="Instance">The instance as loaded, at the version the Commit expects.</param>
/// <param name="Recipe">Its recipe (the long form of its pinned hash).</param>
/// <param name="Trigger">The trigger.</param>
/// <param name="Answers">The matched transition's gate and evidence answers.</param>
/// <param name="Context">The time, epoch, approval, catalog, runtimes, children and timings.</param>
public sealed record DecisionInput(WorkflowInstance Instance, Recipe Recipe, Trigger Trigger, GateAnswers Answers, DecisionContext Context);
