namespace Aw.Evidence.Core.Visual;

/// <summary>What the visual gate decided.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Reasons">The judge's reasons (empty without a judgement).</param>
/// <param name="DiffScore">The fraction of pixels differing from the baseline (1 for a size mismatch); null when not compared.</param>
/// <param name="DiffImage">The diff overlay written for the baseline comparison, when one was made.</param>
/// <param name="Cache">The verdict cache, with any new verdict stored.</param>
public sealed record VisualOutcome(Verdict Verdict, IReadOnlyList<string> Reasons, double? DiffScore, string? DiffImage, VerdictCache Cache);
