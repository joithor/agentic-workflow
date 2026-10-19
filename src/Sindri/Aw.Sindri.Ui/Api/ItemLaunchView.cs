namespace Aw.Sindri.Ui.Api;

/// <summary>An item's launch block (UI spec E-2): what the server lets the app offer. The app renders it and decides nothing.</summary>
/// <param name="Modes">The modes offered, in order: 'open', 'attach', 'resume'. Empty when the item is closed.</param>
/// <param name="DefaultTarget">The agent id an Open would use.</param>
/// <param name="ChosenBy">Which rule chose it: lastUsed, profile or builtIn.</param>
/// <param name="Attach">The live session, or null.</param>
/// <param name="Resumable">The newest ended session with an id, once the item was handed back, or null.</param>
/// <param name="InFlight">The launch id of one still starting, or null.</param>
/// <param name="LastStarted">The newest launch that started, or null.</param>
/// <param name="Confirm">Set when another Open should ask first, or null.</param>
/// <param name="RepoPath">The item's repo checkout, or null.</param>
/// <param name="Blockers">Why an Open would fail now.</param>
internal sealed record ItemLaunchView(
    IReadOnlyList<string> Modes,
    string DefaultTarget,
    string ChosenBy,
    LaunchAttachView? Attach,
    LaunchResumableView? Resumable,
    string? InFlight,
    LaunchLastStartedView? LastStarted,
    LaunchConfirmView? Confirm,
    string? RepoPath,
    IReadOnlyList<LaunchBlockerView> Blockers);
