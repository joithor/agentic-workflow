namespace Aw.Sindri.Ui.Navigation;

/// <summary>What the key that opened an instance's frame wants it to do on arrival; the frame does it once the instance is read.</summary>
internal enum FrameIntent
{
    /// <summary>Nothing: the frame opens as a click would open it.</summary>
    None,

    /// <summary>A on a desk task: focus lands on the output the desk step awaits.</summary>
    PendingOutput,

    /// <summary>X: the frame asks whether to cancel, with its Keep button focused.</summary>
    AskCancel,

    /// <summary>Back from a child's frame: focus lands on the child's entry in the children panel.</summary>
    FocusChild,

    /// <summary>Back from a child's item sheet: focus lands on the child's Open button.</summary>
    FocusChildItem,

    /// <summary>Back from a child's frame that the expanded state's child pointer opened: focus lands on that pointer.</summary>
    FocusPointer,

    /// <summary>A frame opened by stacking (a child entry, the child pointer): focus lands on its first read action once it is read.</summary>
    OpenInitial,

    /// <summary>Back from the recipe sheet the frame opened: focus returns to the frame's Recipe link.</summary>
    FocusRecipe,

    /// <summary>"Answer in the child" with no child to open: the parent's frame opens with focus in its Children panel, when it has one.</summary>
    FocusChildren,

    /// <summary>Take over from the run drawer: the frame opens where the take-over command is; the app sends nothing.</summary>
    TakeOver,

    /// <summary>Hand back from the run drawer: the frame opens where the hand-back command is; the app sends nothing.</summary>
    HandBack,
}
