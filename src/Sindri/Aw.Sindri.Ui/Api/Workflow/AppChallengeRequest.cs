namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>POST /sindri/workflow/challenges/app's body: the action text the human is about to confirm ('approve-all' and the plan's first six digest characters). Human token only; an agent session is refused.</summary>
/// <param name="Action">The action text.</param>
internal sealed record AppChallengeRequest(string Action);
