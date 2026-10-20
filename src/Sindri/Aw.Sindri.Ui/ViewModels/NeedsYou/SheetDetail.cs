namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One labelled line of a Needs-you row's sheet (a service, a hash, an action that is not in the app yet).</summary>
/// <param name="Label">What the line is.</param>
/// <param name="Value">What the line shows; for an action that is not in the app yet it starts with the status prefix, so it is not what a copy takes.</param>
/// <param name="Hint">A caption drawn beside the value, never part of it ("fill in name"); empty for none.</param>
/// <param name="Command">The bare command a copy takes, with no status prefix and no hint; empty for a line that is not a command.</param>
/// <param name="Secondary">True for bookkeeping (a hash, an approve command): the sheet keeps it behind a closed Details.</param>
internal sealed record SheetDetail(string Label, string Value, string Hint = "", string Command = "", bool Secondary = false);
