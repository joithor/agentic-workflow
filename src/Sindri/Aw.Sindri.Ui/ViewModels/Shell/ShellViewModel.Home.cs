#pragma warning disable MA0041, CA1822 // red stubs
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Home's Needs-you list in the shell (<see cref="ShellViewModel"/>, continued; U3' T9).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Home's Needs-you list.</summary>
    public NeedsYouViewModel NeedsYou => throw new NotSupportedException();

    /// <summary>True when the sheet's subject is a Needs-you row.</summary>
    public bool IsNeedsYouSheet => throw new NotSupportedException();

    /// <summary>True when the sheet's subject is a decision.</summary>
    public bool IsDecisionSheet => throw new NotSupportedException();

    /// <summary>Marks every current row seen (Shift+A from anywhere but a text field).</summary>
    internal void MarkAllSeen() => throw new NotSupportedException();
}
