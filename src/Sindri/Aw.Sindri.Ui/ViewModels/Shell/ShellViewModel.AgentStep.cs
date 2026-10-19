using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The links between a Step's frame and its session (<see cref="ShellViewModel"/>, continued; L2): an Agents row or the session sheet opens the Step's frame, and the frame's agent Step opens its session's sheet.</summary>
internal sealed partial class ShellViewModel
{
    private void InitAgentStep()
    {
        Agents.FrameRequested += (_, e) => OpenStepFrame(e.Address);
        AgentSheet.FrameRequested += (_, e) => OpenStepFrame(e.Address);
        Instance.SessionRequested += (_, e) => Navigate(new DeepLink(DeepLinkKind.Session, e.Ref));
    }

    // The way a Workflows row opens a frame: the instance's own link, its first read action focused once the frame is read.
    private void OpenStepFrame(string address)
    {
        Instance.Intend(address, FrameIntent.OpenInitial);
        Navigate(new DeepLink(DeepLinkKind.Workflow, address));
    }
}
