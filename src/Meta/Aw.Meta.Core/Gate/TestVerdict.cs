namespace Aw.Meta.Core.Gate;

/// <summary>The flake classification of one test of a red gate step.</summary>
/// <param name="Test">The test filter the rerun used.</param>
/// <param name="Verdict">Flake or real.</param>
public sealed record TestVerdict(string Test, FlakeVerdict Verdict);
