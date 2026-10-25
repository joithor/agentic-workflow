namespace Aw.Sindri.Core.Events;

/// <summary>A code index build published an index ('codeindex.built', code index spec §8). Counts and words only: no name, path or text beyond the repo. Not public: it names the repo.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="Layers">Each layer's name, status and code.</param>
/// <param name="Files">Indexed files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Resolved edges.</param>
/// <param name="UnresolvedShare">The share of call sites that resolved to nothing.</param>
/// <param name="AmbiguousShare">The share that stayed ambiguous.</param>
/// <param name="EmbeddedPercent">Symbols with a vector, as a percentage of those that should have one (0 to 100).</param>
/// <param name="Milliseconds">How long the build ran.</param>
/// <param name="Incremental">True when it started from the live index.</param>
public sealed record CodeIndexBuilt(string Repo, string Commit, IReadOnlyList<CodeIndexBuiltLayer> Layers, int Files, int Symbols, int Edges, double UnresolvedShare, double AmbiguousShare, int EmbeddedPercent, long Milliseconds, bool Incremental);
