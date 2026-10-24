namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>An eval case file an agent's approval carries (owner q93, 2026-10-23): the server runs it against the registered recipe instead of trusting a caller's say-so.</summary>
/// <param name="Path">The path as the recipe's 'evals:' lists it.</param>
/// <param name="Text">The file's text.</param>
public sealed record EvalFile(string Path, string Text);
