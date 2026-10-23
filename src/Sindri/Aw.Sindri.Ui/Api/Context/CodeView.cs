namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The code lens' answer, as far as the app reads it: the callers and callees the code index gave for the files around the anchors (S-9).</summary>
/// <param name="SchemaVersion">The answer's schema version.</param>
/// <param name="MapVersion">The map version it was read from.</param>
/// <param name="Draft">Whether it is the draft map.</param>
/// <param name="Callers">The edges into the symbols of those files.</param>
/// <param name="Callees">The edges out of them.</param>
internal sealed record CodeView(
    int SchemaVersion,
    string MapVersion,
    bool Draft,
    SymbolsView Callers,
    SymbolsView Callees);
