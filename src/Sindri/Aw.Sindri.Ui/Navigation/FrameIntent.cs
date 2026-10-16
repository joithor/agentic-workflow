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
}
