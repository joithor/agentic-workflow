using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Marks a request record as a job-class command (API spec §2.1, §4.1): starting it is a command (one domain event, 'job.started') that answers
/// <see cref="JobStartedResponse"/> at once; the work runs afterwards outside the command gate, reports progress through
/// <see cref="IJobContextAccessor"/>, and ends with 'job.finished' or 'job.cancelled'. Register it with AddJob (Features) and map it with MapJob (Host).
/// </summary>
/// <typeparam name="TResult">What the work returns when it succeeds; it is kept (bounded, scrubbed) in the job's view.</typeparam>
public interface IJob<TResult> : ICommand<JobStartedResponse>;
