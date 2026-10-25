using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Testing.Symbols;

/// <summary>What the fake code index holds for one repo: its state, the commit it describes, its symbols and call edges. <see cref="CodeSymbolSourceContract"/> seeds every source from these.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">The block's own state, before the as-of comparison.</param>
/// <param name="Commit">The commit the index describes, or null for a missing index.</param>
/// <param name="Symbols">The symbols.</param>
/// <param name="Calls">The call edges.</param>
public sealed record FakeIndexedRepo(ContextRepoName Repo, CodeIndexState State, string? Commit, IReadOnlyList<SymbolSite> Symbols, IReadOnlyList<FakeCall> Calls)
{
    /// <summary>When the index was built.</summary>
    public DateTimeOffset? BuiltAt { get; init; } = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The unavailable layer ('graph') of a <see cref="CodeIndexState.LayerUnavailable"/> index.</summary>
    public string? Layer { get; init; }

    /// <summary>The SND-CODEIDX code that explains the state, or null.</summary>
    public string? Code { get; init; }

    /// <summary>The call sites that resolved to nothing, reported with every callers or callees answer.</summary>
    public int Unresolved { get; init; }
}
