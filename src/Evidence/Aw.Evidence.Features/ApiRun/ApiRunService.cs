using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>
/// Runs an API script: reads and checks it, vets the host, headers and goldens (refusals take no lock), then holds the
/// QA-stack and heavy-job locks while it runs the requests, and writes a scrubbed summary.json on every path once the
/// script has been read.
/// </summary>
internal sealed class ApiRunService(ApiRunFiles files, ApiVetting vetting, RequestRunner requests, CallRunner calls, IMachineLocks locks, TimeProvider clock, IIdGenerator ids)
    : IService<ApiRunRequest, ApiRunResponse>
{
    public async ValueTask<Result<ApiRunResponse>> Handle(ApiRunRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(request.ScriptPath, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var prepared = await files.PrepareRunDirectoryAsync(request.RunDirectory, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var run = new RunFacts(request, ContentHash.Sha256Hex(bytes.Value), RunInstanceId.From(ids.NewId().ToString("D")), clock.GetUtcNow());
        var script = files.Parse(request.ScriptPath, bytes.Value);
        var vetted = script.IsError ? script.Error : (await vetting.VetAsync(request, script.Value, cancellationToken).ConfigureAwait(false)).Match<Result<VettedRun>>(v => v, e => e);
        if (vetted.IsError)
        {
            return await WriteAsync(run, Refused(run, script.IsError ? null : script.Value, vetted.Error), cancellationToken).ConfigureAwait(false);
        }

        var lease = await StackLocks.AcquireAsync(locks, request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            return lease.Error;
        }

        var collected = files.NewCollected();
        await using (lease.Value.ConfigureAwait(false))
        {
            await requests.RunAsync(vetted.Value, collected, cancellationToken).ConfigureAwait(false);
            await calls.RunAsync(vetted.Value, collected, cancellationToken).ConfigureAwait(false);
        }

        return await WriteAsync(run, Finished(run, vetted.Value, collected), cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<Result<ApiRunResponse>> WriteAsync(RunFacts run, ApiSummary summary, CancellationToken cancellationToken) =>
        files.WriteSummaryAsync(run.Request.RunDirectory, summary, cancellationToken);

    private ApiSummary Refused(RunFacts run, ApiScript? script, Error error) =>
        Compose(run, new SummaryParts(script, Host: null, ApiOutcome.Refused, Number.Text($"refused: {error.Code} {error.Message}"), files.NewCollected(), new CodedMessage(error.Code, error.Message), Error: null, PassedEnv: []));

    private ApiSummary Finished(RunFacts run, VettedRun vetted, Collected collected)
    {
        var headline = HeadlineOf(collected);
        var outcome = collected.Error is not null ? ApiOutcome.Errored : collected.Failures.Count > 0 ? ApiOutcome.Failed : ApiOutcome.Passed;
        return Compose(run, new SummaryParts(vetted.Script, vetted.Host?.Origin, outcome, headline, collected, Refusal: null, collected.Error, vetted.PassedEnv));
    }

    private static string HeadlineOf(Collected collected)
    {
        if (collected.Error is not null)
        {
            return "errored: " + collected.Error.Message;
        }

        if (collected.Failures.Count > 0)
        {
            return $"{collected.FirstFailureText} (assertion {Number.Text(collected.FirstFailedAssertion)} of {Number.Text(collected.Assertions)})";
        }

        return collected.Assertions == 1 ? "passed: 1 assertion" : $"passed: {Number.Text(collected.Assertions)} assertions";
    }

    private ApiSummary Compose(RunFacts run, SummaryParts parts) =>
        new(
            ApiSummary.SchemaName, ApiSummary.KindName, RunId.From(RunPath.NameOf(run.Request.RunDirectory)), run.Instance, run.Started, clock.GetUtcNow(),
            run.Request.AppBuild, run.Sha256, run.Request.At, parts.Host, parts.Outcome, files.Safe(parts.Headline, RecordedText.HeadlineLimit), parts.Collected.Steps,
            parts.Collected.Failures, [.. parts.Script?.DeclaredWrites ?? []], parts.PassedEnv, ApiSummary.Unseeded, SeedSha256: null, parts.Refusal, parts.Error, []);

    private sealed record RunFacts(ApiRunRequest Request, string Sha256, RunInstanceId Instance, DateTimeOffset Started);

    private sealed record SummaryParts(ApiScript? Script, string? Host, ApiOutcome Outcome, string Headline, Collected Collected, CodedMessage? Refusal, CodedMessage? Error, IReadOnlyList<string> PassedEnv);
}
