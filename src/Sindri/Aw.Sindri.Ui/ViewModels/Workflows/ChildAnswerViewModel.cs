using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The "Answer in the child" control of a row whose answer a child instance decides: it opens the child's frame when the row
/// is served the child's address, else the parent's frame at its Children panel. A needs-you row is served the child's address when the engine knows it, else neither address:
/// it carries its row id, and the shell opens the instance the row stands for. It decides nothing and copies nothing.
/// </summary>
internal sealed class ChildAnswerViewModel
{
    private ChildAnswerViewModel(string? parentAddress, string? childAddress, string? needsYouRowId, string shownAddress)
    {
        (ParentAddress, ChildAddress, NeedsYouRowId) = (parentAddress, childAddress, needsYouRowId);
        Name = (parentAddress, childAddress) switch
        {
            (_, not null) => Labels.AnswerInChildName(childAddress),
            (not null, null) => Labels.AnswerInChildParentName(parentAddress),
            _ => Labels.AnswerInChildOpensName(shownAddress),
        };
    }

    /// <summary>A pointer on an instance row.</summary>
    /// <param name="parentAddress">The parked instance's address.</param>
    /// <param name="childAddress">The child's address as served on the answer; null or empty for none.</param>
    /// <returns>The control.</returns>
    internal static ChildAnswerViewModel ForInstance(string parentAddress, string? childAddress)
    {
        var child = string.IsNullOrEmpty(childAddress) ? null : childAddress;
        return new ChildAnswerViewModel(parentAddress, child, needsYouRowId: null, child ?? parentAddress);
    }

    /// <summary>A pointer on a needs-you row: it opens the served child's frame, else (none served) the instance the row stands for.</summary>
    /// <param name="rowId">The row's id.</param>
    /// <param name="shownId">What the row shows as its id.</param>
    /// <param name="childAddress">The child's address as the row serves it; null or empty for none.</param>
    /// <returns>The control.</returns>
    internal static ChildAnswerViewModel ForNeedsYouRow(string rowId, string shownId, string? childAddress = null) =>
        new(parentAddress: null, string.IsNullOrEmpty(childAddress) ? null : childAddress, rowId, shownId);

#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    /// <summary>The drawn words.</summary>
    public string Text => Labels.AnswerInChild;
#pragma warning restore CA1822, MA0041

    /// <summary>The accessible name: the drawn words, then the address it opens.</summary>
    public string Name { get; }

    /// <summary>The parked instance's address; null on a needs-you row.</summary>
    internal string? ParentAddress { get; }

    /// <summary>The child's address as served; null when the row was served none.</summary>
    internal string? ChildAddress { get; }

    /// <summary>The needs-you row's id; null on an instance row.</summary>
    internal string? NeedsYouRowId { get; }

    /// <summary>True when it opens the child's frame.</summary>
    internal bool OpensChild => ChildAddress is not null;

    /// <summary>The instance whose frame opens; null on a needs-you row served no child.</summary>
    internal string? Target => ChildAddress ?? ParentAddress;

    /// <summary>What the frame does on arrival: the child's asks for its first read action, the parent's puts focus in its Children panel.</summary>
    internal FrameIntent Intent => OpensChild ? FrameIntent.OpenInitial : FrameIntent.FocusChildren;

    /// <summary>Where it goes; null on a needs-you row served no child.</summary>
    internal DeepLink? Link => Target is { } target ? new DeepLink(DeepLinkKind.Workflow, target) : null;
}
