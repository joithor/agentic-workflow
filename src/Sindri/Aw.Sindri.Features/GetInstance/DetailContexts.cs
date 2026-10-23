using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Builds the detail view's context at the time of the read: the 'asOf' is that time, and the recipe carries its display name (setup-once spec §7.7).</summary>
/// <param name="clock">The time the status text and the 'asOf' are read at.</param>
/// <param name="names">The recipes' display names; null serves none.</param>
internal sealed class DetailContexts(TimeProvider clock, IRecipeDisplayNames? names = null)
{
    public InstanceDetailContext Of(string title, ParentRef? parent, ViewCounts counts, RecipeId recipe, IReadOnlyList<StepRun> stepRuns, SessionHandle? session = null, IReadOnlyDictionary<string, DeskReviewTarget>? reviewTargets = null) =>
        new(title, parent, counts, clock.GetUtcNow(), stepRuns, names?.Of(recipe), session, reviewTargets);
}
