using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxDown;

/// <summary>
/// Stops the project's containers (`docker compose stop`: it removes no container, volume or image, and Sindri's files stay). Stops whatever containers `docker compose ps` shows live (running, restarting or paused) for the project (also after a start that failed, or with an unreadable state.json) and does nothing
/// when none runs or the fetched files were never laid down. With the compose override or .env gone it stops by the compose project label alone (`docker compose -p {project} stop`) and says so. Refuses a Docker that points at another machine (SND-ONYX-010), and takes the same lock as `onyx up`, so it never stops a stack that is being started.
/// </summary>
internal sealed class OnyxDownService(IOnyxWorkspace workspace, OnyxFacts facts, IContainerRuntime runtime) : IService<OnyxDownRequest, OnyxDownResponse>
{
    public ValueTask<Result<OnyxDownResponse>> Handle(OnyxDownRequest request, CancellationToken cancellationToken)
    {
        var lockAttempt = workspace.TryLock();
        if (lockAttempt.IsError)
        {
            return ValueTask.FromResult<Result<OnyxDownResponse>>(lockAttempt.Error);
        }

        using var held = lockAttempt.Value;
        if (held is null)
        {
            return ValueTask.FromResult<Result<OnyxDownResponse>>(SindriErrors.CommandBusy.With("another `sindri onyx` command is running; wait for it to finish, then run this again"));
        }

        var result = Stop(cancellationToken);
        return ValueTask.FromResult(result.IsError ? ComposeFailures.WithHint(result.Error, "run `sindri onyx down` again") : result);
    }

    private Result<OnyxDownResponse> Stop(CancellationToken cancellationToken)
    {
        var compose = workspace.Read(facts.Paths.Compose);
        if (compose.IsError)
        {
            return compose.Error;
        }

        if (compose.Value is null)
        {
            return new OnyxDownResponse(WasRunning: false, [], []);   // the fetched files never landed: no project to ask docker about
        }

        var project = facts.Project;
        var missing = MissingRenderedFiles();
        if (missing.IsError)
        {
            return missing.Error;
        }

        List<string> warnings = facts.ForeignProjectWarning(workspace.ReadState()) is { } foreign ? [foreign] : [];
        if (missing.Value.Count > 0)
        {
            // Compose can't read the project without its files, but it still knows the containers by their project label.
            project = ComposeProject.ByLabel(project.Name);
            warnings.Add($"{string.Join(" and ", missing.Value)} missing; stopping by the project label (`docker compose -p {project.Name} stop`)");
        }

        return StopLive(project, warnings, cancellationToken);
    }

    // What runs is what docker says this home's project has, whatever state.json recorded: a start that failed halfway leaves containers up too.
    private Result<OnyxDownResponse> StopLive(ComposeProject project, List<string> warnings, CancellationToken cancellationToken)
    {
        var containers = facts.Containers(project, cancellationToken);
        if (containers.IsError)
        {
            return containers.Error;
        }

        IReadOnlyList<string> running = [.. containers.Value.Where(static c => ComposeContainer.IsLive(c.State)).Select(static c => c.Service).Order(StringComparer.Ordinal)];
        if (running.Count == 0)
        {
            return new OnyxDownResponse(WasRunning: false, [], warnings);
        }

        var stopped = runtime.Stop(project, cancellationToken);
        return stopped.IsError ? stopped.Error : new OnyxDownResponse(WasRunning: true, running, warnings);
    }

    // The rendered files compose reads beside the pinned compose file: the names of those that are gone (an unreadable one is an error, not a missing one).
    private Result<List<string>> MissingRenderedFiles()
    {
        List<string> missing = [];
        foreach (var path in new[] { facts.Paths.Override, facts.Paths.Env })
        {
            var read = workspace.Read(path);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value is null)
            {
                missing.Add(path[(path.LastIndexOf('/') + 1)..]);
            }
        }

        return missing;
    }
}
