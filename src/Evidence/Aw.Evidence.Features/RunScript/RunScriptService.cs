using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.RunScript;

/// <summary>
/// Runs a UI script: reads and checks it, vets the host and the steps (refusals take no lock), then holds the QA-stack
/// and heavy-job locks for the whole browser run, deletes an earlier summary.json, writes the new one and releases the
/// locks on every path.
/// </summary>
internal sealed class RunScriptService(ScriptFiles files, IMachineLocks locks, ScriptRun run, RunVerdict verdict, TimeProvider clock)
    : IService<RunScriptRequest, RunScriptResponse>
{
    public async ValueTask<Result<RunScriptResponse>> Handle(RunScriptRequest request, CancellationToken cancellationToken)
    {
        var loaded = await files.ReadAsync(request.ScriptPath, cancellationToken).ConfigureAwait(false);
        var host = loaded.Bind(_ => HostGuard.Resolve(request.Host, request.AllowPreviewHost));
        var vetted = host.Bind(h => StepGuard.ForRun(loaded.Value.Script.Steps, h, request.AllowWrites));
        if (vetted.IsError)
        {
            return vetted.Error;
        }

        var lease = await StackLocks.AcquireAsync(locks, request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            return lease.Error;
        }

        await using (lease.Value.ConfigureAwait(false))
        {
            return await RunAsync(request, loaded.Value, host.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<RunScriptResponse>> RunAsync(RunScriptRequest request, LoadedScript loaded, ResolvedHost host, CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        var created = await files.PrepareRunDirectoryAsync(request.RunDirectory, cancellationToken).ConfigureAwait(false);
        if (created.IsError)
        {
            return created.Error;
        }

        var script = loaded.Script;
        var log = new InvocationLog();
        if (script.Planning is { } planning)
        {
            log.RecordPlanning(planning);
        }

        var driven = await run.DriveAsync(script, new RunTarget(host, request.RunDirectory, request.AllowWrites), log, cancellationToken).ConfigureAwait(false);
        if (driven.IsError)
        {
            return driven.Error;
        }

        var visual = await verdict.DecideAsync(request, script, driven.Value, log, cancellationToken).ConfigureAwait(false);
        var summary = new RunSummary(
            RunSummary.SchemaName, RunId.From(RunPath.NameOf(request.RunDirectory)), started, clock.GetUtcNow(), script.Route, script.Role, host.Origin,
            request.AppBuild, request.Fixtures, loaded.Sha256, script.Planning?.Pr, script.Viewports, driven.Value.Steps, visual,
            driven.Value.LintFindings, log.All, script.Planning, driven.Value.Evidence);
        var written = await files.WriteSummaryAsync(request.RunDirectory, summary, cancellationToken).ConfigureAwait(false);
        return written.Map(_ => new RunScriptResponse(summary, summary.AllStepsPassed()));
    }
}
