using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The build's <see cref="IEngineRules"/>: each member calls its static rule.</summary>
internal sealed class EngineRules : IEngineRules
{
    private EngineRules()
    {
    }

    /// <summary>The one instance.</summary>
    public static EngineRules Default { get; } = new();

    /// <inheritdoc/>
    public WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context) =>
        FrameRules.Enter(instance, recipe, state, context);

    /// <inheritdoc/>
    public WorkflowInstance Suspend(WorkflowInstance instance, SuspendOutcome outcome, DateTimeOffset now) =>
        FrameRules.Suspend(instance, outcome, now);

    /// <inheritdoc/>
    public Result<PopResult> Pop(WorkflowInstance instance, Recipe recipe, WorkflowInstanceId? child, DateTimeOffset now) =>
        FrameRules.Pop(instance, recipe, child, now);

    /// <inheritdoc/>
    public Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor, DecisionContext context) =>
        FrameRules.TakeOver(instance, actor, context);

    /// <inheritdoc/>
    public WorkflowInstance Cancel(WorkflowInstance instance, DateTimeOffset now) => FrameRules.Cancel(instance, now);

    /// <inheritdoc/>
    public LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark) =>
        LoopRules.Reenter(instance, recipe, state, mark);

    /// <inheritdoc/>
    public bool AtCap(WorkflowInstance instance, Recipe recipe, string state) => LoopRules.AtCap(instance, recipe, state);

    /// <inheritdoc/>
    public bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current) => LoopRules.NoProgress(loop, previous, current);

    /// <inheritdoc/>
    public BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens) => BudgetRules.Spend(instance, recipe, tokens);

    /// <inheritdoc/>
    public StartRequest CreateChild(WorkflowInstance parent, ChildWait wait, TriggerKey key, string actor) =>
        ChildPropagation.Create(parent, wait, key, actor);

    /// <inheritdoc/>
    public ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change) => ChildPropagation.ToParent(parent, recipe, change);

    /// <inheritdoc/>
    public WorkflowInstance Release(WorkflowInstance parent, Recipe recipe, WorkflowInstanceId child, DateTimeOffset now) =>
        ChildPropagation.Release(parent, recipe, child, now);

    /// <inheritdoc/>
    public InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after) => EventFold.Delta(before, after);
}
