using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>What the frame shows when a read fails (<see cref="InstanceFrameViewModel"/>, continued): a first read ends in a problem state with Back, a later one keeps what is shown.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private void Failed(Error error)
    {
        if (Phase == FramePhase.Shown)
        {
            // What was shown stays; a refusal says the instance is no longer served, any other failure changes nothing.
            Note = error.Kind == ErrorKind.NotFound ? Labels.InstanceGone : Note;
        }
        else if (error.Kind == ErrorKind.NotFound)
        {
            Problem(FramePhase.NotFound, error.Message);
        }
        else if (string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            Problem(FramePhase.Unreadable, Labels.StripUnreadable);
        }
        else
        {
            Problem(FramePhase.Refused, Labels.InstanceRefused(error));   // a refusal, a rebuild, a timeout: never a skeleton that never ends
        }
    }

    private void Problem(FramePhase phase, string text)
    {
        if (Phase == FramePhase.Shown)
        {
            Note = Labels.InstanceGone;
            return;
        }

        ProblemText = text;
        var asked = _intent is not null;
        _intent = null;   // nothing to act on
        Phase = phase;
        if (asked)
        {
            Focus(FrameFocus.Back, key: null);   // a key opened or returned to this frame: focus never lands nowhere, Back is all there is
        }
    }
}
