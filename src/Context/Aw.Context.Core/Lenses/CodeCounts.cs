namespace Aw.Context.Core.Lenses;

/// <summary>How many files and symbols the code band holds before the cut.</summary>
/// <param name="Files">Files.</param>
/// <param name="Symbols">Symbols (0 until Phase 9).</param>
/// <param name="FeaturesWithNoCode">Scope features with no confirmed 'implements' edge from a file.</param>
public sealed record CodeCounts(int Files, int Symbols, int FeaturesWithNoCode);
