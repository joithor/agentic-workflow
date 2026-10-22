namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A symbol the code index holds.</summary>
/// <param name="Ref">'path#name'.</param>
/// <param name="Name">The name.</param>
/// <param name="Kind">The kind ('method', 'function').</param>
/// <param name="Path">The file.</param>
/// <param name="StartLine">First line.</param>
/// <param name="EndLine">Last line.</param>
/// <param name="Signature">The signature, when there is one.</param>
/// <param name="Exported">Whether other code can see it.</param>
internal sealed record SymbolSiteView(
    string Ref,
    string Name,
    string Kind,
    string Path,
    int StartLine,
    int EndLine,
    string? Signature,
    bool Exported);
