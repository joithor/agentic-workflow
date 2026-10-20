using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

/// <summary>Registers job-class commands (API P6): the contract the context lane's SyncSources and BuildContextMap use.</summary>
public static class SindriJobServiceCollectionExtensions
{
    /// <summary>
    /// Adds <typeparamref name="TRequest"/> as a job. Starting it is a command behind the usual chain whose service only starts the job; the work,
    /// <typeparamref name="TService"/>, is registered raw (scoped) and run by the runner outside the gate. <typeparamref name="TResult"/> must be in
    /// the host's JSON context (the result is kept in the job's view, bounded and scrubbed).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The idempotency key of a start within its kind; a start whose kind and key is running is SND-JOB-409.</param>
    /// <param name="summary">The one-line summary of a result.</param>
    /// <param name="precheck">A check run inside the gate before the start is accepted (an unknown service is a synchronous 404), or null.</param>
    /// <param name="repo">The repo a start by a human is about (a session's job is always in its own repo), or null for a job of no repo.</param>
    /// <typeparam name="TRequest">The job request; its catalog row is class 'job'.</typeparam>
    /// <typeparam name="TResult">What the work returns.</typeparam>
    /// <typeparam name="TService">The work.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJob<TRequest, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services, Func<TRequest, JobKey> key, Func<TResult, string> summary,
        Func<TRequest, CancellationToken, ValueTask<Result<Unit>>>? precheck = null, Func<TRequest, RepoName?>? repo = null)
        where TRequest : class, IJob<TResult>
        where TService : class, IService<TRequest, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(summary);
        services.TryAddSingleton<JobRegistry>();
        services.TryAddSingleton<IRunningJobs>(static sp => sp.GetRequiredService<JobRegistry>());
        services.TryAddSingleton<IJobKinds, RegisteredJobKinds>();
        services.TryAddScoped<JobAdmitter<TRequest>>();
        services.TryAddScoped<IValidator<TRequest>, JobAcceptAllValidator<TRequest>>();
        _ = services
            .AddScoped<TService>()
            .AddSingleton(new JobDefinition(typeof(TRequest), request => key((TRequest)request),
                request => repo?.Invoke((TRequest)request),
                (request, ct) => precheck is null ? ValueTask.FromResult<Result<Unit>>(Unit.Value) : precheck((TRequest)request, ct),
                async (resolve, request, ct) => await Work<TRequest, TResult, TService>(resolve, (TRequest)request, summary, ct).ConfigureAwait(false)));
        return services.AddCommand<TRequest, JobStartedResponse, JobStartService<TRequest, TResult>>();
    }

    private static async ValueTask<JobWorkResult> Work<TRequest, TResult, TService>(Func<Type, object> resolve, TRequest request, Func<TResult, string> summary, CancellationToken ct)
        where TRequest : class
        where TService : class, IService<TRequest, TResult>
    {
        var done = await ((TService)resolve(typeof(TService))).Handle(request, ct).ConfigureAwait(false);
        if (done.IsError)
        {
            return new JobWorkResult(done.Error, string.Empty, Result: null);
        }

        var options = (JsonSerializerOptions)resolve(typeof(JsonSerializerOptions));
        return new JobWorkResult(Error: null, summary(done.Value), JsonSerializer.SerializeToElement(done.Value, options.GetTypeInfo(typeof(TResult))));
    }
}
