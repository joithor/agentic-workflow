namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>Where focus lands when the frame opens: always a read action, never a command that changes the instance (§11.2).</summary>
internal enum FrameFocus
{
    /// <summary>The subject's Open in link, when the subject has a link.</summary>
    OpenIn,

    /// <summary>Copy status.</summary>
    CopyStatus,

    /// <summary>The output a desk step awaits (A on a desk task).</summary>
    PendingOutput,

    /// <summary>Keep, the safe answer of Cancel's question (X on a row).</summary>
    Keep,

    /// <summary>Back, when there is nothing else to read.</summary>
    Back,

    /// <summary>The children panel's entry for the child (its title button) the user came back from.</summary>
    ChildEntry,

    /// <summary>The children panel's Open button for the child whose item the user came back from.</summary>
    ChildItem,

    /// <summary>The expanded state's child pointer, which the user came back from.</summary>
    ChildPointer,

    /// <summary>The header's Recipe link (Back from the recipe sheet it opened).</summary>
    Recipe,

    /// <summary>The first child's entry in the children panel (the answer is the child's and no child was named).</summary>
    Children,
}
