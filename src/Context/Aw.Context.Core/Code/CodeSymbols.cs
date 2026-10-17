using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>Callers or callees: a recorded state until the code index exists (S-9).</summary>
/// <param name="State">'unavailable' until then.</param>
/// <param name="Reason">'index-not-built'.</param>
/// <param name="Items">The symbols (none until S-9).</param>
public sealed record CodeSymbols(SymbolsState State, string Reason, IReadOnlyList<string> Items)
{
    /// <summary>The reason word of a missing code index.</summary>
    public const string IndexNotBuilt = "index-not-built";

    /// <summary>What S-CL serves for callers and callees.</summary>
    public static CodeSymbols Unavailable { get; } = new(SymbolsState.Unavailable, IndexNotBuilt, []);
}
