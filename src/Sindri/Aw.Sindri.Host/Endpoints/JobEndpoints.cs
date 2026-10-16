using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Maps job-class commands (API P6): the generic route a lane adds with one line, next to its AddJob registration and its JobRows.Job catalog row.</summary>
internal static class JobEndpoints
{
    /// <summary>
    /// Maps <paramref name="route"/> (under /sindri) as a POST that binds <typeparamref name="TRequest"/> from the JSON body, calls its chained start
    /// service and answers <see cref="JobStartedResponse"/> (status and ProblemDetails as every command endpoint). A job whose request is built from route
    /// values maps its own endpoint instead, as RunService does.
    /// </summary>
    /// <param name="group">The commands group (Origin refused before binding).</param>
    /// <param name="route">The route, for example '/context/sources/sync'.</param>
    /// <param name="bind">Builds the request from the call when it is not the JSON body (RunService reads its '{name}' route value); null binds the body.</param>
    /// <typeparam name="TRequest">The job request.</typeparam>
    /// <typeparam name="TResult">What the work returns.</typeparam>
    /// <returns>The same group.</returns>
    internal static RouteGroupBuilder MapJob<TRequest, TResult>(this RouteGroupBuilder group, string route, Func<HttpContext, Result<TRequest>>? bind = null)
        where TRequest : class, IJob<TResult>
    {
        ArgumentNullException.ThrowIfNull(group);
        var mapped = group.MapPost(route, async http =>
        {
            var json = http.RequestServices.GetRequiredService<JsonSerializerOptions>();
            TRequest? request = null;
            if (bind is not null)
            {
                var bound = bind(http);
                if (bound.IsError)
                {
                    await HttpResults.Problem(bound.Error).ExecuteAsync(http).ConfigureAwait(false);
                    return;
                }

                request = bound.Value;
            }

            try
            {
                request ??= await JsonSerializer.DeserializeAsync(http.Request.Body, (JsonTypeInfo<TRequest>)json.GetTypeInfo(typeof(TRequest)), http.RequestAborted).ConfigureAwait(false);
            }
            catch (JsonException e)
            {
                await HttpResults.Problem(SindriErrors.InvalidInput.With("the request body isn't valid: " + e.Message)).ExecuteAsync(http).ConfigureAwait(false);
                return;
            }

            var service = http.RequestServices.GetRequiredService<IService<TRequest, JobStartedResponse>>();
            var started = request is null
                ? HttpResults.Problem(SindriErrors.InvalidInput.With("the request body is empty"))
                : (await service.Handle(request, http.RequestAborted).ConfigureAwait(false)).ToHttpResult();
            await started.ExecuteAsync(http).ConfigureAwait(false);
        });
        _ = mapped.WithMetadata(new JobRouteMetadata(typeof(TRequest)));
        return group;
    }
}
