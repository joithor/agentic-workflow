using System.Text.Json;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The code lens' callers or callees panel (S-9): the edges the index gave, the words that say where they came from, and the call sites it could not resolve.</summary>
internal sealed class CallsPanelViewModel
{
    /// <summary>The panel over what the lens served.</summary>
    /// <param name="callers">Callers (true) or callees.</param>
    /// <param name="symbols">The served calls object.</param>
    internal CallsPanelViewModel(bool callers, SymbolsView symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        Title = callers ? Labels.ContextCallersTitle : Labels.ContextCalleesTitle;
        (Caption, Dimmed) = SymbolsCaption.Of(symbols);
        Unavailable = string.Equals(symbols.State, "unavailable", StringComparison.Ordinal);
        Rows = Unavailable ? [] : [.. symbols.Items.Select(Row)];
        Unresolved = !Unavailable && symbols.Unresolved > 0 ? Labels.ContextCallsUnresolved(symbols.Unresolved) : null;
        Empty = !Unavailable && Rows.Count == 0 ? Labels.ContextCallsNone : null;
    }

    /// <summary>"Callers" or "Callees".</summary>
    public string Title { get; }

    /// <summary>"from 7c3e1a2", "from abcdef0, not 1234567", "index not built".</summary>
    public string Caption { get; }

    /// <summary>Whether the rows are drawn dimmed.</summary>
    public bool Dimmed { get; }

    /// <summary>Whether the index cannot answer.</summary>
    public bool Unavailable { get; }

    /// <summary>The edges, strongest first as served.</summary>
    public IReadOnlyList<CallRow> Rows { get; }

    /// <summary>"3 calls not resolved", or null when none.</summary>
    public string? Unresolved { get; }

    /// <summary>"none found" when the index answered with no edges, else null.</summary>
    public string? Empty { get; }

    /// <summary>Whether the unresolved count is shown.</summary>
    public bool HasUnresolved => Unresolved is not null;

    /// <summary>Whether the none-found line is shown.</summary>
    public bool HasEmpty => Empty is not null;

    /// <summary>The panel's opacity: dimmed when the rows come from another commit than the map's.</summary>
    public double Opacity => Dimmed ? 0.6 : 1.0;

    private static CallRow Row(JsonElement item)
    {
        var symbol = item.TryGetProperty("symbol", out var s) ? s : default;
        return new CallRow(Text(symbol, "name"), Text(symbol, "path"), Labels.Dots(Text(item, "basis"), Text(item, "coverage")),
            item.TryGetProperty("depth", out var depth) && depth.TryGetInt32(out var level) ? level : 1);
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
