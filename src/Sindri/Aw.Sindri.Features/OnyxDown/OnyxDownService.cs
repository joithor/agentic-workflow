using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxDown;

/// <summary>
/// Stops the project's containers (`docker compose stop`: it removes no container, volume or image, and Sindri's files stay). Does nothing when `onyx up` never finished its start
/// step, refuses a Docker that points at another machine (SND-ONYX-010), and takes the same lock as `onyx up`, so it never stops a stack that is being started.
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
        if (workspace.ReadState() is not { } state || !state.Steps.ContainsKey(OnyxSteps.Start))
        {
            return new OnyxDownResponse(WasRunning: false);   // never set up: nothing to stop
        }

        var endpoint = runtime.Inspect(cancellationToken).Endpoint;
        if (!OnyxPreflight.IsLocalEndpoint(endpoint))
        {
            return OnyxPreflight.RemoteDocker(endpoint);
        }

        var containers = facts.Containers(cancellationToken);
        if (containers.IsError)
        {
            return containers.Error;
        }

        var stopped = runtime.Stop(facts.Paths.Project, cancellationToken);
        return stopped.IsError ? stopped.Error : new OnyxDownResponse(containers.Value.Any(static c => c.State == "running"));
    }
}
