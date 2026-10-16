using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
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
        if (Refused(request) is { } refusal)
        {
            return refusal;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CompleteStep needs a caller");
        }

        var prepared = await Prepare(request, caller.Actor, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (address, outputs) = prepared.Value;
        var nonce = request.Nonce ?? nonces.Issue();
        var reportSha = ReportSha(request, outputs.ToDictionary(static o => o.Key, static o => Convert.ToHexStringLower(SHA256.HashData(o.Value.Span))));
        var subject = address.Value + "\n" + reportSha;
        var claim = await nonces.Claim(nonce, subject, cancellationToken).ConfigureAwait(false);
        if (claim.IsError)
        {
            return claim.Error;
        }

        var stored = claim.Value == NonceClaim.Fresh && await Approved(address, cancellationToken).ConfigureAwait(false) is { IsError: true } refused   // only a fresh claim is checked, as ApplyCommand does
            ? refused.Error
            : await Store(outputs, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return await GiveBack(nonce, claim.Value, stored.Error, cancellationToken).ConfigureAwait(false);
        }

        var report = new StepCompleted(caller.Actor, request.Run, request.Outcome, stored.Value, Tokens: 0, StepRuntime.Desk, reportSha);
        return await Send(address, new SentReport(nonce, subject, claim.Value), report, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The instance the request names and the outputs to report: the request's plus the engine-filled ones.</summary>
    private async ValueTask<Result<(InstanceAddress Address, IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> Outputs)>> Prepare(CompleteStepRequest request, string actor, CancellationToken cancellationToken)
    {
        var resolved = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var outputs = await WithEngineFilled(resolved.Value, request, actor, cancellationToken).ConfigureAwait(false);
        return outputs.IsError ? outputs.Error : (resolved.Value, outputs.Value);
    }

    private static Error? Refused(CompleteStepRequest request) =>
        request.Outputs.Where(static o => o.Value.Length > IFactStore.MaxBytes).Select(static o => o.Key.Value).FirstOrDefault() is { } large
            ? SindriErrors.InvalidInput.With($"{large} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)")
            : null;

    /// <summary>A report is not exempt from the approval check (spec §5.2): the instance's recipe must still hold, else nothing is stored or sent.</summary>
    private async ValueTask<Result<Unit>> Approved(InstanceAddress address, CancellationToken cancellationToken)
    {
        var view = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var approval = await recipes.Check(view.Value.Recipe, cancellationToken).ConfigureAwait(false);
        return approval.IsError ? approval.Error : ApprovalRefusal.Of(approval.Value, view.Value.Recipe.Id, view.Value.Recipe.Hash, address);
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

    /// <summary>
    /// The report's outputs with the ones the engine writes (DT spec §8.1: 'fact:decision' of the choice Steps, from --outcome
    /// and the caller's actor, provider "desk"). A report that gives one itself is refused before anything is claimed. When
    /// the instance can't be read, or its open task isn't the one reported on, the outputs are the request's: the send answers.
    /// </summary>
    private async ValueTask<Result<IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>>>> WithEngineFilled(InstanceAddress address, CompleteStepRequest request, string actor, CancellationToken cancellationToken)
    {
        var view = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError || view.Value.Task is not { } task || task.Run != request.Run)
        {
            return Results.Success(request.Outputs);
        }

        var filled = EngineFilledOutputs.For(task.Outputs, request.Outcome, "desk", actor);
        var written = request.Outputs.Keys.Where(filled.ContainsKey).ToList();
        if (written.Count > 0)
        {
            return SindriErrors.InvalidInput.With($"{written[0].Value} is written by the engine on '{task.Step.Value}' (fix: drop --output {written[0].Value})");
        }

        var merged = new Dictionary<FactKey, ReadOnlyMemory<byte>>(request.Outputs);
        foreach (var (fact, text) in filled)
        {
            merged[fact] = Encoding.UTF8.GetBytes(text);
        }

        return merged;
    }

    private async ValueTask<Result<IReadOnlyDictionary<FactKey, FactValue>>> Store(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, FactValue>();
        foreach (var (key, content) in outputs)
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
