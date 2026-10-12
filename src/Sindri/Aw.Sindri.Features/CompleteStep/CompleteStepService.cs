using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete' (engine spec rev 8.1 §5.2, §5.3, §6.3): checks each output is at most 1 MiB, claims the nonce for
/// this exact report (SND-WF-009 when it named another command, before anything is stored or sent), stores the outputs
/// owner-only, and sends the 'complete' Update with the nonce as its id. The workflow's validator answers a stale run
/// token (SND-WF-005) or a malformed report (SND-WF-008) synchronously; such a refusal gives the nonce back, so a
/// refused report consumes nothing. A report already applied with this nonce returns the first outcome, marked
/// 'already applied'. Without a nonce one is issued (the stand-in for API P1's IssueNonce).
/// </summary>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="facts">Where the outputs are stored.</param>
/// <param name="nonces">The nonce table.</param>
internal sealed class CompleteStepService(IInstanceRuntime runtime, IFactStore facts, ICommandNonces nonces)
    : IService<CompleteStepRequest, CompleteStepResponse>
{
    public async ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Outputs.Where(static o => o.Value.Length > IFactStore.MaxBytes).Select(static o => o.Key.Value).FirstOrDefault() is { } large)
        {
            return SindriErrors.InvalidInput.With($"{large} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)");
        }

        var nonce = request.Nonce ?? nonces.Issue();
        var shas = request.Outputs.ToDictionary(static o => o.Key, static o => Convert.ToHexStringLower(SHA256.HashData(o.Value.Span)));
        var reportSha = ReportSha(request, shas);
        var claim = await nonces.Claim(nonce, request.Address.Value + "\n" + reportSha, cancellationToken).ConfigureAwait(false);
        if (claim.IsError)
        {
            return claim.Error;
        }

        var stored = await Store(request, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return await GiveBack(nonce, claim.Value, stored.Error, cancellationToken).ConfigureAwait(false);
        }

        var report = new StepCompleted(request.Actor, request.Run, request.Outcome, stored.Value, Tokens: 0, StepRuntime.Desk, reportSha);
        var sent = await runtime.Send(request.Address, nonce, report, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error.Kind is ErrorKind.Unavailable or ErrorKind.Timeout
                ? sent.Error   // it may have applied: keep the claim, so the retry with this nonce is deduplicated
                : await GiveBack(nonce, claim.Value, sent.Error, cancellationToken).ConfigureAwait(false);
        }

        var applied = await nonces.Applied(nonce, cancellationToken).ConfigureAwait(false);
        return applied.IsError ? applied.Error : new CompleteStepResponse(sent.Value, nonce, claim.Value);
    }

    /// <summary>A refused report consumes nothing (spec §5.2, W12): the claim is given back, unless the nonce had already applied it.</summary>
    private async ValueTask<Result<CompleteStepResponse>> GiveBack(CommandNonce nonce, bool alreadyApplied, Error refusal, CancellationToken cancellationToken)
    {
        if (!alreadyApplied)
        {
            _ = await nonces.Release(nonce, cancellationToken).ConfigureAwait(false);
        }

        return refusal;
    }

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
    private static string ReportSha(CompleteStepRequest request, Dictionary<FactKey, string> shas)
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
