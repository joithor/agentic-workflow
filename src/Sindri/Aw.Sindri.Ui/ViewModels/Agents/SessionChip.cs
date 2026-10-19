namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A session state as the page draws it: a glyph and its word. The word is always there, so the glyph is never the only signal.</summary>
/// <param name="Glyph">The mark.</param>
/// <param name="Word">The state's word in capitals.</param>
internal readonly record struct SessionChip(string Glyph, string Word)
{
    /// <summary>The glyph and the word, as the row reads them ("● RUNNING").</summary>
    public string Text => Glyph + " " + Word;
}
