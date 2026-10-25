namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>One line of the dialog's lists that has a name and, optionally, a second line (an own-look workflow, a refused one, a skipped one).</summary>
/// <param name="Name">The workflow, or the line's first words.</param>
/// <param name="Text">The second line; empty for none.</param>
/// <param name="Cli">A command to copy; empty for none.</param>
/// <param name="Extra">A third line (what is newly allowed); empty for none.</param>
internal sealed record ApproveAllLine(string Name, string Text, string Cli = "", string Extra = "")
{
    /// <summary>The name a screen reader hears.</summary>
    internal string AccessibleName => (Text.Length > 0 ? Name + ": " + Text : Name) + (Extra.Length > 0 ? ". " + Extra : string.Empty);

    /// <summary>True when there is a command to copy.</summary>
    internal bool HasCli => Cli.Length > 0;

    /// <summary>The Copy button's word.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    internal string CopyLabel => Labels.Copy;
#pragma warning restore CA1822, MA0041

    /// <summary>The Copy button's accessible name: it says which workflow's command it copies.</summary>
    internal string CopyName => Labels.Copy + ": " + Name;

    /// <summary>True when there is a third line.</summary>
    internal bool HasExtra => Extra.Length > 0;
}
