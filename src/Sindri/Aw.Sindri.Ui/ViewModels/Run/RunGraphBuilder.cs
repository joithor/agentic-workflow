using Aw.Sindri.Ui.Api.Workflow;

#pragma warning disable MA0182 // until slice 3 places the run page

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Builds the run canvas's graph from what is served (F3 spec 3 §4), with a fallback for everything not served yet.</summary>
internal static class RunGraphBuilder
{
    /// <summary>The graph of a line run.</summary>
    /// <param name="line">The line instance.</param>
    /// <param name="recipe">The line's recipe, or null when it is not read.</param>
    /// <param name="children">The stage children read in full, by instance id.</param>
    /// <returns>The graph.</returns>
    internal static RunGraph Build(InstanceDetail line, RecipeView? recipe, IReadOnlyDictionary<string, InstanceDetail> children) => throw new NotSupportedException();
}

#pragma warning restore MA0182
