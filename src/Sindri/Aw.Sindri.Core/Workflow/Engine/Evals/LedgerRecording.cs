using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>What a served instance's ledger events ran against, which <see cref="EvalLedgerRecorder"/> reads to write them back as an eval case.</summary>
/// <param name="Recipe">The instance's recipe, which says which state waits on a child and which Step each state opens.</param>
/// <param name="Children">The pinned child recipes, whose declared outputs a child's end may carry.</param>
/// <param name="Catalog">The build's block descriptors, which say what outputs a Step's report has to carry.</param>
public sealed record LedgerRecording(Recipe Recipe, IChildRecipeResolver Children, BlockCatalog Catalog);
