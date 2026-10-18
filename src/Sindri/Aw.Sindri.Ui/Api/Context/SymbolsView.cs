using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the symbol lens says: a state word, a reason and the items.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Reason">Reason, as served.</param>
/// <param name="Items">Items, as served.</param>
internal sealed record SymbolsView(
    string State,
    string? Reason,
    IReadOnlyList<JsonElement> Items);
