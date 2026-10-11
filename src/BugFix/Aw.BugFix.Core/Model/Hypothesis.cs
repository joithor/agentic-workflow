namespace Aw.BugFix.Core.Model;

/// <summary>One row of the investigation's hypotheses table.</summary>
/// <param name="Number">The row's number (#).</param>
/// <param name="Text">The hypothesis.</param>
/// <param name="Files">The cause-site files it names.</param>
/// <param name="Result">What the investigation found.</param>
public sealed record Hypothesis(int Number, string Text, IReadOnlyList<string> Files, HypothesisResult Result);
