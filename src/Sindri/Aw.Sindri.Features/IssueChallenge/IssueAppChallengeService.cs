using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Issues the app's challenge (setup-once spec §4 items 1 and 2) for exactly three actions: 'approve profile &lt;hash6&gt;', 'approve &lt;recipe&gt; &lt;version&gt;' and 'approve-all &lt;digest6&gt;'. Anything else
/// is SND-AUTH-002: it stays a human decision at a terminal. The terminal's issuing plans each action and builds its preview, so the app sees what the terminal sees; the challenge is on the app channel,
/// and approve-all's is held, with its text as the typed alternative.
/// </summary>
/// <param name="issuer">The terminal route's issuing.</param>
internal sealed partial class IssueAppChallengeService(IssueChallengeService issuer) : IService<IssueAppChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueAppChallengeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Action.Trim();
        return Allowed.IsMatch(text)
            ? issuer.Issue(text, ChallengeChannel.App, cancellationToken)
            : ValueTask.FromResult<Result<IssueChallengeResponse>>(SindriErrors.AuthRefused.With(
                $"The app confirms only 'approve profile <hash6>', 'approve <recipe> <version>' and 'approve-all <digest6>'; '{text}' is a human decision at a terminal"));
    }

    [GeneratedRegex(@"^(approve profile [0-9a-f]{6}|approve-all [0-9a-f]{6}|approve [^\s@]+ [0-9]{1,9})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Allowed { get; }
}
