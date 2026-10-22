using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The words that say where symbols came from and whether to trust them (code index spec §7.3): never colour alone, and a cell is never blank without a reason.</summary>
internal static class SymbolsCaption
{
    /// <summary>The caption of a symbol lens.</summary>
    /// <param name="symbols">What the lens served.</param>
    /// <returns>The words, and whether the cells are dimmed (served from another commit than the map's, or from a stale index).</returns>
    internal static (string Text, bool Dimmed) Of(SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        if (string.Equals(symbols.State, "unavailable", StringComparison.Ordinal))
        {
            return (Unavailable(symbols.Reason), false);
        }

        if (symbols.Commit is not { } commit)
        {
            return (Unavailable(reason: null), false);
        }

        if (symbols.AsOfMismatch && symbols.AsOfCommit is { } asOf)
        {
            return (Labels.ContextSymbolsFromNot(commit, asOf), true);
        }

        return string.Equals(symbols.State, "stale", StringComparison.Ordinal) ? (Labels.ContextSymbolsFromStale(commit), true) : (Labels.ContextSymbolsFrom(commit), false);
    }

    private static string Unavailable(string? reason) => reason?.Replace('-', ' ') switch
    {
        "index damaged" => Labels.ContextSymbolsDamaged,
        "index building" => Labels.ContextSymbolsBuilding,
        "not configured" => Labels.ContextSymbolsNotConfigured,
        _ => Labels.ContextSymbolsNotBuilt,
    };
}
