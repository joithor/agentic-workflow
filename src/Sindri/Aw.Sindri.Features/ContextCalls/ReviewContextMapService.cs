using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One review action (context spec §8.7, README X63). The desk variants act at once: one override row each (the bulk accept: one per
/// deterministic entry), recorded as 'context.map.reviewed'. The protected variants (apply-file, merge, add, delete) first show what
/// they would do and issue the challenge 'review &lt;verb&gt; &lt;hash6&gt;', bound to the request's fields (and, for a file, its content);
/// with an answer that redeems it, they act. The chain has already refused a caller the verb's class doesn't allow.
/// </summary>
internal sealed class ReviewContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events,
    ContextChallenges challenges) : IService<ReviewContextMapRequest, ReviewContextMapResponse>
{
    public async ValueTask<Result<ReviewContextMapResponse>> Handle(ReviewContextMapRequest request, CancellationToken cancellationToken)
    {
        var input = ReviewInput.Read(request);
        if (input.IsError)
        {
            return input.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        if (input.Value.IsProtected)
        {
            var confirmed = await Confirm(request, input.Value, scope.Value, cancellationToken).ConfigureAwait(false);
            if (confirmed.IsError || confirmed.Value.Preview is not null)
            {
                return confirmed.IsError ? confirmed.Error : confirmed.Value.Preview!;   // why: the check above found a preview
            }
        }

        return await Act(request.Verb, input.Value, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Performs the action and records 'context.map.reviewed' when it wrote rows.</summary>
    private async ValueTask<Result<ReviewContextMapResponse>> Act(string verb, ReviewInput input, ContextScope scope, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var done = input.Path is { } path
            ? await engine.ApplyReviewFile(path, scope, cancellationToken).ConfigureAwait(false)
            : await engine.ApplyReview(input.Decision!, scope, cancellationToken).ConfigureAwait(false);   // why: a non-file input carries its decision
        if (done.IsError || done.Value.Written.Count == 0)
        {
            return done.IsError ? done.Error : new ReviewContextMapResponse(done.Value, Sequence: null);
        }

        var payload = new ContextMapReviewed(verb, done.Value.Written.Count, done.Value.Undecided);
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapReviewed, payload, $"{EventTypes.ContextMapReviewed}:{done.Value.Written[0].Value}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ReviewContextMapResponse(done.Value, appended.Value.Sequence);
    }

    /// <summary>A protected variant: the preview and its challenge when there is no answer, or an empty confirmation once the answer redeemed it, or the refusal.</summary>
    private async ValueTask<Result<Confirmation>> Confirm(ReviewContextMapRequest request, ReviewInput input, ContextScope scope, CancellationToken cancellationToken)
    {
        var fingerprint = await Fingerprint(request, input, scope, cancellationToken).ConfigureAwait(false);
        if (fingerprint.IsError)
        {
            return fingerprint.Error;
        }

        var action = new ChallengeAction($"review {request.Verb} {fingerprint.Value.Hash[..6]}");
        var subject = new ChallengeSubject($"review:{request.Verb}:{fingerprint.Value.Hash}", Digest: null);
        if (request.Answer is null)
        {
            return new Confirmation(new ReviewContextMapResponse(Result: null, Sequence: null, challenges.Issue(action, subject), fingerprint.Value.Summary));
        }

        return challenges.Refuse(request.Answer, action, subject, $"sindri context map review {request.Verb}") is { } refused ? refused : new Confirmation(Preview: null);
    }

    /// <summary>The hash of what the action is about (its fields, or the review file's content) and the words that say what it would do.</summary>
    private async ValueTask<Result<(string Hash, string Summary)>> Fingerprint(ReviewContextMapRequest request, ReviewInput input, ContextScope scope, CancellationToken cancellationToken)
    {
        var content = string.Empty;
        var summary = request.Verb switch
        {
            "merge" => $"merge {request.Node} into {request.Other} in the draft map",
            "add" => $"add {request.Node} ('{request.Label}'){(request.Other is null ? string.Empty : " under " + request.Other)} to the draft map",
            "delete" => $"delete {request.Node} from the draft map",
            _ => string.Empty,
        };
        if (input.Path is { } path)
        {
            var file = await engine.PreviewReviewFile(path, scope, cancellationToken).ConfigureAwait(false);
            if (file.IsError)
            {
                return file.Error;
            }

            content = file.Value.Hash;
            summary = string.Create(CultureInfo.InvariantCulture, $"apply the review file {path} ({file.Value.Entries} entries, {file.Value.Decided} decided) to the latest draft");
        }

        var text = $"{request.Verb}\n{request.Node}\n{request.Other}\n{request.Label}\n{input.Path}\n{content}";
        return (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))), summary);
    }

    private sealed record Confirmation(ReviewContextMapResponse? Preview);
}
