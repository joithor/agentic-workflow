using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete' (engine spec rev 8.1 §5.2, §5.3, §6.3): checks each output is at most 1 MiB, claims the nonce for
/// this exact report (SND-WF-009 when it named another command, before anything is stored or sent), checks that the
/// instance's recipe is still approved (SND-RECIPE-005, -006; the claim is given back), stores the outputs
/// owner-only, and sends the 'complete' Update with the nonce as its id. The workflow's validator answers a stale run
/// token (SND-WF-005) or a malformed report (SND-WF-008) synchronously; such a refusal gives the nonce back, so a
/// refused report consumes nothing. A report already applied with this nonce returns the first outcome, marked
/// 'already applied'. Without a nonce one is issued (the stand-in for API P1's IssueNonce).
/// </summary>
/// <param name="directory">Resolves the instance the request names.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="recipes">The approval check (spec §5.2: complete is not exempt).</param>
/// <param name="facts">Where the outputs are stored.</param>
/// <param name="nonces">The nonce table.</param>
/// <param name="callers">The caller the API chain classed: its actor reports.</param>
internal sealed class CompleteStepService(IInstanceDirectory directory, IInstanceRuntime runtime, IApprovedRecipes recipes, IFactStore facts, ICommandNonces nonces, ICallerContextAccessor callers)
    : IService<CompleteStepRequest, CompleteStepResponse>
{
    public async ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Outputs.Where(static o => o.Value.Length > IFactStore.MaxBytes).Select(static o => o.Key.Value).FirstOrDefault() is { } large)
        {
            return SindriErrors.InvalidInput.With($"{large} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)");
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CompleteStep needs a caller");
        }

        var resolved = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var address = resolved.Value;
        var nonce = request.Nonce ?? nonces.Issue();
        var reportSha = ReportSha(request, request.Outputs.ToDictionary(static o => o.Key, static o => Convert.ToHexStringLower(SHA256.HashData(o.Value.Span))));
        var subject = address.Value + "\n" + reportSha;
        var claim = await nonces.Claim(nonce, subject, cancellationToken).ConfigureAwait(false);
        if (claim.IsError)
        {
            return claim.Error;
        }

        var stored = claim.Value == NonceClaim.Fresh && await Approved(address, cancellationToken).ConfigureAwait(false) is { IsError: true } refused   // only a fresh claim is checked, as ApplyCommand does
            ? refused.Error
            : await Store(request, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return await GiveBack(nonce, claim.Value, stored.Error, cancellationToken).ConfigureAwait(false);
        }

        var report = new StepCompleted(caller.Actor, request.Run, request.Outcome, stored.Value, Tokens: 0, StepRuntime.Desk, reportSha);
        return await Send(address, new SentReport(nonce, subject, claim.Value), report, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A report is not exempt from the approval check (spec §5.2): the instance's recipe must still hold, else nothing is stored or sent.</summary>
    private async ValueTask<Result<Unit>> Approved(InstanceAddress address, CancellationToken cancellationToken)
    {
        var view = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var approval = await recipes.Check(view.Value.Recipe, cancellationToken).ConfigureAwait(false);
        return approval.IsError ? approval.Error : ApprovalRefusal.Of(approval.Value, view.Value.Recipe, address);
    }

    /// <summary>Sends the report as the Update keyed by the nonce; a refusal gives back a claim this call created, an unconfirmed send keeps it.</summary>
    private async ValueTask<Result<CompleteStepResponse>> Send(InstanceAddress address, SentReport sentReport, StepCompleted report, CancellationToken cancellationToken)
    {
        var sent = await runtime.Send(address, sentReport.Nonce, report, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error.Kind is ErrorKind.Unavailable or ErrorKind.Timeout
                ? sent.Error   // it may have applied: keep the claim, so the retry with this nonce resends the same Update id
                : await GiveBack(sentReport.Nonce, sentReport.Claim, sent.Error, cancellationToken).ConfigureAwait(false);
        }

        var applied = await nonces.Applied(sentReport.Nonce, sentReport.Subject, cancellationToken).ConfigureAwait(false);
        return applied.IsError ? applied.Error : new CompleteStepResponse(sent.Value, sentReport.Nonce, sentReport.Claim == NonceClaim.Applied);
    }

    /// <summary>A refused report consumes nothing (spec §5.2, W12): a claim this call created is given back; a pending or applied one stays, as its Update id was used.</summary>
    private async ValueTask<Result<CompleteStepResponse>> GiveBack(CommandNonce nonce, NonceClaim claim, Error refusal, CancellationToken cancellationToken)
    {
        if (claim == NonceClaim.Fresh)
        {
            _ = await nonces.Release(nonce, cancellationToken).ConfigureAwait(false);
        }

        return refusal;
    }

    /// <summary>The nonce a report went out with, the subject it claimed it for and what the claim found.</summary>
    private readonly record struct SentReport(CommandNonce Nonce, string Subject, NonceClaim Claim);

    private async ValueTask<Result<IReadOnlyDictionary<FactKey, FactValue>>> Store(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, FactValue>();
        foreach (var (key, content) in request.Outputs)
        {
            var put = await facts.Put(content, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }

            stored[key] = put.Value;
        }

        return stored;
    }

    /// <summary>The SHA-256 of the canonical report (run, outcome, each output's key and content digest in key order), so two different reports never share a key.</summary>
    internal static string ReportSha(CompleteStepRequest request, Dictionary<FactKey, string> shas)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"run {request.Run.Value}\noutcome {request.Outcome.Value}\n");
        foreach (var (key, sha) in shas.OrderBy(static s => s.Key.Value, StringComparer.Ordinal))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"output {key.Value} {sha}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
