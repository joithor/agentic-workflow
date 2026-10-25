using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// A check of one kind of input fact at the start of an instance: the start runs every registered check over the facts the caller seeded, before it writes anything, and refuses a start
/// whose fact a check finds wrong (the real e2e started 'workflow.scoping' with a 'fact:scopeInput' that lacked its repo, and nothing said so). A check answers null for a fact that is
/// not its own and for one that passes. The recipe format is untouched: an input keeps its 'type'.
/// </summary>
public interface IStartFactCheck
{
    /// <summary>Checks a fact.</summary>
    /// <param name="recipe">The recipe being started.</param>
    /// <param name="fact">The fact's key.</param>
    /// <param name="content">The fact's content as the caller sent it.</param>
    /// <returns>What is wrong with it, or null when it passes or the check is not about it.</returns>
    public StartFactProblem? Check(RecipeId recipe, FactKey fact, ReadOnlyMemory<byte> content);
}
