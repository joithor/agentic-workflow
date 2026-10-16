namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One labelled line of a Needs-you row's sheet (a service, a hash, an action that is not in the app yet).</summary>
/// <param name="Label">What the line is.</param>
/// <param name="Value">What was served for it: the text a copy takes.</param>
/// <param name="Hint">A caption drawn beside the value, never part of it ("fill in name"); empty for none.</param>
internal sealed record SheetDetail(string Label, string Value, string Hint = "");
