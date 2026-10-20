using System.Globalization;
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

        JobTicket ticket;
        RepoName? repo;
        try
        {
            var kind = JobKind.From(ActionClassifier.Classify(catalog, request).Action.Value);
            var key = admitted.Value.Definition.KeyOf(request);
            ticket = new JobTicket(JobId.New(ids), kind, key);
            repo = caller.Class == CallerClass.Session ? caller.Repo : admitted.Value.Definition.RepoOf(request);   // a session works in its own repo; a human says which repo the work is about
        }
        catch
        {
            admitter.Rollback(request);   // the admission took something the work will never use
            throw;
        }

        if (!registry.TryClaim(ticket.Kind, ticket.Key, ticket.Job, out var running))
        {
            admitter.Rollback(request);
            return Conflict(ticket.Kind, ticket.Key, running);
        }

        return await Begin(ticket, admitted.Value, request, caller, repo).ConfigureAwait(false);
    }

    /// <summary>
    /// Records the start and hands the job over once its claim is held, whatever the request does: an aborted POST must neither tear the append nor leave
    /// the claim behind, and a throw releases it.
    /// </summary>
    private async ValueTask<Result<JobStartedResponse>> Begin(JobTicket ticket, AdmittedJob admitted, TRequest request, CallerContext caller, RepoName? repo)
    {
        try
        {
            var recorded = await recorder.Record(ticket, caller, admitted.Service, repo, CancellationToken.None).ConfigureAwait(false);
            if (recorded.IsError)
            {
                registry.Release(ticket.Job);
                admitter.Rollback(request);
                return recorded.Error;
            }

            await runner.Start(new JobStart(ticket.Job, ticket.Kind, ticket.Key, admitted.Definition, request, caller, recorded.Value.Grant, admitted.Service, recorded.Value.Cause, repo), CancellationToken.None).ConfigureAwait(false);
            return Response(caller, ticket.Job, recorded.Value.Sequence);
        }
        catch
        {
            registry.Release(ticket.Job);
            admitter.Rollback(request);   // once the runner has the job it releases the claim itself; releasing a claim already gone is a no-op
            throw;
        }
    }

    private static JobStartedResponse Response(CallerContext caller, JobId job, long sequence) =>
        new(new CommandAccepted(caller.CommandId, sequence), job,
            new JobLinks($"/sindri/jobs/{job.Value}", $"/sindri/jobs/{job.Value}/cancel", string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={sequence - 1}&types=job.*")));

    private static Error Conflict(JobKind kind, JobKey key, JobId running) => JobConflicts.Running(kind, key, running);
}
