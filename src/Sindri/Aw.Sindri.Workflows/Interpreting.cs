using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the interpreter's loop decides with: the recipe and the decision context it built from 'LoadRecipe'.</summary>
/// <param name="Recipe">The recipe's long form.</param>
/// <param name="Decisions">The approval answer, the catalog, the runtimes and the child recipes.</param>
internal sealed record Interpreting(Recipe Recipe, DecisionContext Decisions);
