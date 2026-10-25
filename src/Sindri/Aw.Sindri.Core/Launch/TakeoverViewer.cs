namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What a takeover did to open the Step's live session (issue 1004): served on the takeover's response as 'viewer' and recorded by 'launch.takeover-opened'.
/// With <see cref="Outcome"/> failed or none-usable, <see cref="Reason"/> says why and <see cref="Attach"/> is the command a person runs themselves.
/// </summary>
/// <param name="Kind">The viewer kind that was tried ('warp', 'exec-template', 'print'), or 'none' when no viewer is configured or detected.</param>
/// <param name="Label">What a person calls it ('Warp'), or null.</param>
/// <param name="Outcome">What it came to.</param>
/// <param name="Reason">Why nothing opened, for failed and none-usable; otherwise null.</param>
/// <param name="Attach">The command that attaches to the live session: ["tmux","-L","sindri-launch","attach","-t","=snd-LCH-…"].</param>
public sealed record TakeoverViewer(string Kind, string? Label, TakeoverViewerOutcome Outcome, string? Reason, IReadOnlyList<string> Attach);
