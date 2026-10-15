using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// Composes the API spec section 3.8 view records (E-20, E-22, E-23) from the engine's instance and recipe. A stub: every
/// method throws until its unit (API P11 U2 to U5, U9) writes it, after engine P2b (StatePathOrder) and P4 (the instance
/// query). The records and goldens are the contract it must produce. The signatures are provisional: U2 sets the inputs
/// (likely the engine's query view and the path order) once engine #192 merges, and the PublicAPI lines move with them.
/// </summary>
public static class WorkflowViewComposer
{
    /// <summary>One list row.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its pinned recipe.</param>
    /// <param name="since">When it entered its state.</param>
    /// <returns>The row.</returns>
    public static InstanceSummary Summary(WorkflowInstance instance, Recipe recipe, DateTimeOffset since) =>
        throw new NotSupportedException("API P11 U2: the composer waits for engine P2b and P4");

    /// <summary>The full view of one instance.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its pinned recipe.</param>
    /// <param name="asOf">When it was read.</param>
    /// <returns>The view.</returns>
    public static InstanceDetail View(WorkflowInstance instance, Recipe recipe, DateTimeOffset asOf) =>
        throw new NotSupportedException("API P11 U2: the composer waits for engine P2b and P4");

    /// <summary>The status fields of one instance.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its pinned recipe.</param>
    /// <returns>The fields.</returns>
    public static StatusFields Status(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("API P11 U3: status waits for engine P4's status layout");

    /// <summary>The fact rows of one instance: inputs, then outputs.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its pinned recipe.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<FactRow> Facts(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("API P11 U5: facts wait for fact provenance");

    /// <summary>The child groups of one instance.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its pinned recipe.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<ChildGroup> Children(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("API P11 U9: children wait for engine P4's child view");
}
