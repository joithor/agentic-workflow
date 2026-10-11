using Aw.Evidence.Core.Visual;

namespace Aw.Evidence.Core.Runs;

/// <summary>The run's visual verdict.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Reasons">The judge's reasons.</param>
/// <param name="DiffScore">Fraction of pixels differing from the baseline; null when not compared.</param>
/// <param name="DiffImage">The baseline diff overlay (absolute path), when written.</param>
public sealed record RunVisual(Verdict Verdict, IReadOnlyList<string> Reasons, double? DiffScore, string? DiffImage);
