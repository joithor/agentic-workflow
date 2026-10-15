namespace Aw.Context.Core.Lenses;

/// <summary>The symbol column of the code band: file-level only until the code index exists (Phase 9).</summary>
/// <param name="State">'unavailable' until then.</param>
/// <param name="Reason">'index not built'.</param>
/// <param name="Items">The symbols (none until Phase 9).</param>
public sealed record LensSymbols(SymbolsState State, string Reason, IReadOnlyList<string> Items)
{
    /// <summary>What S-4 serves everywhere: no code index.</summary>
    public static LensSymbols Unavailable { get; } = new(SymbolsState.Unavailable, "index not built", []);
}
