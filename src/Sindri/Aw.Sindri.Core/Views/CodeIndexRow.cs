using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Views;

/// <summary>A repo's newest built code index in the 'code-index' view (code index spec §8): what its 'codeindex.built' said, dated by the event. Counts and words only.</summary>
/// <param name="Repo">The repo; the row's key.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="Layers">Each layer's name, status and code.</param>
/// <param name="Files">Indexed files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Resolved edges.</param>
/// <param name="UnresolvedShare">The share of call sites that resolved to nothing.</param>
/// <param name="AmbiguousShare">The share that stayed ambiguous.</param>
/// <param name="EmbeddedPercent">Symbols with a vector, as a percentage of those that should have one.</param>
/// <param name="Milliseconds">How long the build ran.</param>
/// <param name="Incremental">True when it started from the live index.</param>
/// <param name="BuiltAt">When the event was recorded.</param>
/// <param name="Sequence">The ledger sequence of that event.</param>
public sealed record CodeIndexRow(string Repo, string Commit, IReadOnlyList<CodeIndexBuiltLayer> Layers, int Files, int Symbols, int Edges, double UnresolvedShare, double AmbiguousShare,
    int EmbeddedPercent, long Milliseconds, bool Incremental, DateTimeOffset BuiltAt, long Sequence);
