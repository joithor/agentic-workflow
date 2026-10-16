using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The chained service of a job-class command (API P6, plan Unit C): inside the gate it checks the running cap (SND-JOB-002), the definition's admission,
/// claims (kind, key) (SND-JOB-409 with 'runningJob'), appends 'job.started' and hands the work to <see cref="IJobRunner"/>. It never runs the work.
/// </summary>
/// <param name="admitter">Finds this request's definition and runs its checks.</param>
/// <param name="registry">The running claims.</param>
/// <param name="runner">Runs the work.</param>
/// <param name="recorder">Appends 'job.started'.</param>
/// <param name="ids">Mints the job's id.</param>
/// <param name="callers">The starting caller.</param>
/// <param name="catalog">Names the job's kind (the request's action).</param>
/// <typeparam name="TRequest">The job request.</typeparam>
/// <typeparam name="TResult">What the work returns.</typeparam>
internal sealed class JobStartService<TRequest, TResult>(JobAdmitter<TRequest> admitter, JobRegistry registry, IJobRunner runner, JobStartRecorder recorder,
    IIdGenerator ids, ICallerContextAccessor callers, ICommandCatalog catalog) : IService<TRequest, JobStartedResponse>
    where TRequest : IJob<TResult>
{
    public async ValueTask<Result<JobStartedResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("starting a job needs a caller");
        }

        if (runner.Running >= JobLimits.MaxRunning)
        {
            return SindriErrors.JobsRateLimited.With(string.Create(CultureInfo.InvariantCulture, $"{JobLimits.MaxRunning} jobs are running (fix: wait for one to end, or cancel one)"));
        }

        var admitted = await admitter.Admit(request, cancellationToken).ConfigureAwait(false);
        if (admitted.IsError)
        {
            return admitted.Error;
        }

        var kind = JobKind.From(ActionClassifier.Classify(catalog, request).Action.Value);
        var definition = admitted.Value.Definition;
        var key = definition.KeyOf(request);
        var job = JobId.New(ids);
        var repo = caller.Class == CallerClass.Session ? caller.Repo : definition.RepoOf(request);   // a session works in its own repo; a human says which repo the work is about
        if (!registry.TryClaim(kind, key, job, out var running))
        {
            return Conflict(kind, key, running);
        }

        var recorded = await recorder.Record(new JobTicket(job, kind, key), caller, admitted.Value.Service, repo, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            registry.Release(job);
            return recorded.Error;
        }

        await runner.Start(new JobStart(job, kind, key, definition, request, caller, recorded.Value.Grant, admitted.Value.Service, recorded.Value.Cause, repo), cancellationToken).ConfigureAwait(false);
        return Response(caller, job, recorded.Value.Sequence);
    }

    private static JobStartedResponse Response(CallerContext caller, JobId job, long sequence) =>
        new(new CommandAccepted(caller.CommandId, sequence), job,
            new JobLinks($"/sindri/jobs/{job.Value}", $"/sindri/jobs/{job.Value}/cancel", string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={sequence - 1}&types=job.*")));

    private static Error Conflict(JobKind kind, JobKey key, JobId running)
    {
        using var id = JsonDocument.Parse($"\"{running.Value}\"");   // a job id is 'JOB-' and hex digits: nothing to escape
        return SindriErrors.JobConflict.With($"A {kind.Value} job is already running for {key.Value}").WithExtension("runningJob", id.RootElement.Clone());
    }
}
