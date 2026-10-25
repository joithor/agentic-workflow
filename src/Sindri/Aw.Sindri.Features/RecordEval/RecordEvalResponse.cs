namespace Aw.Sindri.Features.RecordEval;

/// <summary>The recorded case.</summary>
/// <param name="Text">The case as eval lines (JSONL), ready to list in a recipe's 'evals'.</param>
public sealed record RecordEvalResponse(string Text);
