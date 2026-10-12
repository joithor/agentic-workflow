namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>Run a recipe's eval cases in-process, or scaffold one (spec §6.3, §9.2, W27).</summary>
/// <param name="Path">The recipe file; its evals are read relative to its directory.</param>
/// <param name="Mode">Run the cases, or scaffold a happy-path case.</param>
public sealed record EvalRecipeRequest(string Path, EvalMode Mode = EvalMode.Run);
