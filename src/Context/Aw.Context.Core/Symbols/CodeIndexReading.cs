using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Symbols;

/// <summary>Pure rules for reading the code index (code index spec §7.3): the as-of comparison, a head's rows and the lens states the block's states map onto.</summary>
public static class CodeIndexReading
{
    /// <summary>Whether two commits name the same commit; either may be abbreviated (seven characters or more).</summary>
    /// <param name="left">A commit.</param>
    /// <param name="right">Another commit.</param>
    /// <returns>True when one is a prefix of the other.</returns>
    public static bool Same(string left, string right) => throw new NotSupportedException();

    /// <summary>The head of an index read against the map's commit: a readable index that is not at that commit is <see cref="CodeIndexState.Stale"/> (both commits kept), computed at read time.</summary>
    /// <param name="state">The block's own state (head moved, checkout off main, layer down, ...).</param>
    /// <param name="commit">The commit the index describes, or null.</param>
    /// <param name="builtAt">When it was built, or null.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="layer">The unavailable layer, or null.</param>
    /// <param name="code">The explaining SND-CODEIDX code, or null.</param>
    /// <returns>The head.</returns>
    public static CodeIndexHead Head(CodeIndexState state, string? commit, DateTimeOffset? builtAt, string? asOf, string? layer, string? code) => throw new NotSupportedException();

    /// <summary>Whether the index and the map are at different commits.</summary>
    /// <param name="head">The head.</param>
    /// <returns>True when both commits are known and differ.</returns>
    public static bool AsOfMismatch(CodeIndexHead head) => throw new NotSupportedException();

    /// <summary>What every row of an answer carries: 'stale' (and the map's commit when it differs) or 'ok'.</summary>
    /// <param name="head">The head the rows were read under.</param>
    /// <returns>The row state and the as-of commit to show, or null.</returns>
    public static (SymbolsState State, string? AsOfCommit) Stamp(CodeIndexHead head) => throw new NotSupportedException();

    /// <summary>The 'code-index' row of <c>searched.sources</c> for a head.</summary>
    /// <param name="head">The head.</param>
    /// <returns>ok, degraded (stale, checkout-not-on-main, or the layer), unavailable (index-not-built, index-damaged, index-building) or not-configured.</returns>
    public static SearchedSource Row(CodeIndexHead head) => throw new NotSupportedException();

    /// <summary>The row for a read that failed: degraded, with the error's code.</summary>
    /// <param name="code">The error's code.</param>
    /// <returns>The row.</returns>
    public static SearchedSource FailedRow(string code) => throw new NotSupportedException();

    /// <summary>The callers or callees of the code lens for a head and the edges read under it.</summary>
    /// <param name="head">The head.</param>
    /// <param name="rows">The edges (none when the index is unreadable).</param>
    /// <param name="unresolved">The call sites that resolved to nothing.</param>
    /// <returns>The state, reason and rows, with both commits when the index is not at the map's.</returns>
    public static CodeSymbols Calls(CodeIndexHead head, IReadOnlyList<CallEdgeRow> rows, int unresolved) => throw new NotSupportedException();

    /// <summary>The file map's 'symbols' object for a head and the files read under it.</summary>
    /// <param name="head">The head.</param>
    /// <param name="files">The rows of the files listed.</param>
    /// <returns>The state, reason and per-file symbols, with both commits when the index is not at the map's.</returns>
    public static LensSymbols FileMap(CodeIndexHead head, IReadOnlyList<FileSymbolsRow> files) => throw new NotSupportedException();

    /// <summary>The head that limits an answer over several repos: the least healthy one.</summary>
    /// <param name="heads">The heads, at least one.</param>
    /// <returns>The one to report.</returns>
    public static CodeIndexHead Worst(IReadOnlyList<CodeIndexHead> heads) => throw new NotSupportedException();
}
