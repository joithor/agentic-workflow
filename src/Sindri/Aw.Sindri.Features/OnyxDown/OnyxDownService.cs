using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxDown;

/// <summary>
/// Stops the project's containers (`docker compose stop`: it removes no container, volume or image, and Sindri's files stay). Stops whatever containers `docker compose ps` shows running for the project (also after a start that failed, or with an unreadable state.json) and does nothing
/// when none runs or the fetched files were never laid down. Refuses a Docker that points at another machine (SND-ONYX-010), and takes the same lock as `onyx up`, so it never stops a stack that is being started.
/// </summary>
internal sealed class OnyxDownService(IOnyxWorkspace workspace, OnyxFacts facts, IContainerRuntime runtime) : IService<OnyxDownRequest, OnyxDownResponse>
{
    public ValueTask<Result<OnyxDownResponse>> Handle(OnyxDownRequest request, CancellationToken cancellationToken)
    {
        using var held = workspace.TryLock();
        if (held is null)
        {
            return ValueTask.FromResult<Result<OnyxDownResponse>>(SindriErrors.CommandBusy.With("another `sindri onyx` command is running; wait for it to finish, then run this again"));
        }

        return ValueTask.FromResult(Stop(cancellationToken));
    }

    private Result<OnyxDownResponse> Stop(CancellationToken cancellationToken)
    {
        if (workspace.Read(facts.Paths.Compose) is null)
        {
            return new OnyxDownResponse(WasRunning: false, []);   // the fetched files never landed: no project to ask docker about
        }

        var endpoint = runtime.Inspect(cancellationToken).Endpoint;
        if (!OnyxPreflight.IsLocalEndpoint(endpoint))
        {
            return OnyxPreflight.RemoteDocker(endpoint);
        }

        // What runs is what docker says the project has, whatever state.json recorded: a start that failed halfway leaves containers up too.
        var containers = facts.Containers(cancellationToken);
        if (containers.IsError)
        {
            return containers.Error;
        }

        IReadOnlyList<string> running = [.. containers.Value.Where(static c => c.State == "running").Select(static c => c.Service).Order(StringComparer.Ordinal)];
        if (running.Count == 0)
        {
            return new OnyxDownResponse(WasRunning: false, []);
        }

        var stopped = runtime.Stop(facts.Paths.Project, cancellationToken);
        return stopped.IsError ? stopped.Error : new OnyxDownResponse(WasRunning: true, running);
    }
}
