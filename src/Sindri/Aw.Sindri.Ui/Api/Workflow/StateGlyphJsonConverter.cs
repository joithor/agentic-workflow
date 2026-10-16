namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Reads and writes a <see cref="StateGlyph"/> as its wire word; a glyph this build doesn't know (or a non-string) reads as pending.</summary>
internal sealed class StateGlyphJsonConverter() : WordEnumJsonConverter<StateGlyph>(
    [(StateGlyph.Done, "done"), (StateGlyph.Active, "active"), (StateGlyph.Pending, "pending")],
    StateGlyph.Pending);
