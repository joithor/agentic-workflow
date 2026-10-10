using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>A question's settings from config.json, when it has an entry there.</summary>
/// <param name="Enabled">False escalates every call with 'question-disabled'.</param>
/// <param name="Threshold">Overrides the question's own threshold when set.</param>
/// <param name="Providers">Restricts and reorders the chain for this question; names not in the chain are dropped.</param>
public sealed record QuestionOverride(bool Enabled, double? Threshold = null, IReadOnlyList<ProviderName>? Providers = null);
