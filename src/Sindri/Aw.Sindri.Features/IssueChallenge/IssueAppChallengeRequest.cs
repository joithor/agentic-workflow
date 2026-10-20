using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Ask for a challenge the Sindri app confirms (setup-once spec §4 item 1, POST /workflow/challenges/app; desk, the human token only): the action text is one of
/// 'approve profile &lt;hash6&gt;', 'approve &lt;recipe&gt; &lt;version&gt;' and 'approve-all &lt;digest6&gt;'; any other text is SND-AUTH-002.
/// </summary>
/// <param name="Action">The action text.</param>
public sealed record IssueAppChallengeRequest(string Action) : ICommand<IssueChallengeResponse>;
