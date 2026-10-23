using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Each repo's index state, for the caller's repos only; a named repo outside them is the uniform not-found (SND-CODEIDX-013).</summary>
internal sealed class GetCodeIndexStatusService(CodeIndexScopes scopes, ICodeIndex index, IRepoHeads heads, IJobViews jobs, TimeProvider clock) : IService<GetCodeIndexStatusRequest, CodeIndexStatusView>
{
    public async ValueTask<Result<CodeIndexStatusView>> Handle(GetCodeIndexStatusRequest request, CancellationToken cancellationToken)
    {
        var repo = CodeIndexInputs.Repo(request.Repo);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (repo.IsError || scope.IsError)
        {
            return repo.IsError ? repo.Error : scope.Error;
        }

        var answer = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        var repos = repo.Value is { } wanted ? answer.Value.Repos.Where(r => r.Repo == wanted).ToList() : [.. answer.Value.Repos];
        if (repo.Value is not null && repos.Count == 0)
        {
            // The facade's own words, so a repo outside the scope reads like one never built.
            return CodeIndexErrors.IndexNotBuilt.With(CodeIndexErrors.IndexNotBuilt.Title);
        }

        return new CodeIndexStatusView(repos) { Health = await Overlay(scope.Value, repos, cancellationToken).ConfigureAwait(false) };
    }

    // Stale, building and waiting come from the live heads and the build jobs; either read failing leaves the file state as it is.
    private async ValueTask<IReadOnlyList<RepoHealth>> Overlay(CodeIndexScope scope, IReadOnlyList<RepoStatus> repos, CancellationToken cancellationToken)
    {
        var live = await heads.Heads(scope.Repos, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, JobKind.From("BuildCodeIndex"), cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return [.. repos.Select(r => RepoHealth.Of(r, live.IsError ? null : live.Value.FirstOrDefault(h => h.Repo == r.Repo), builds.IsError ? [] : [.. builds.Value.Where(j => IndexBuildWords.IsBuildOf(j, r.Repo.Value))], now))];
    }
}
