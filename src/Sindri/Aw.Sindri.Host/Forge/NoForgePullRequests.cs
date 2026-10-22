using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Host.Forge;

/// <summary>The pull requests of a Sindri whose approved profile names no forge: every call answers SND-FORGE-001 and nothing is sent anywhere.</summary>
internal sealed class NoForgePullRequests : IPullRequests
{
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken) => None<PullView>();

    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken) => None<string>();

    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken) => None<PullPushOutcome>();

    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken) => None<int>();

    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken) => None<OpenedPull?>();

    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken) => None<OpenedPull>();

    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken) => None<Unit>();

    private static ValueTask<Result<T>> None<T>() => ValueTask.FromResult<Result<T>>(SindriErrors.NoForge.With("the approved profile has no workflow.forge.adapter"));
}
