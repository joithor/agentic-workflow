using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetFileMap;

/// <summary>
/// The direct children of a repo path with their status (spec §8.9): the repo's files at HEAD (never a denied path) against the
/// map the caller may read, built by FileMapBuilder. A repo outside the profile, outside 'context.repos' or outside a
/// session's scope is SND-CONTEXT-045 with one answer for every reason, so a scope never reveals what is outside it.
/// </summary>
internal sealed class GetFileMapService(MapReader reader, IRepoReader repos) : IService<GetFileMapRequest, FileMapView>
{
    public async ValueTask<Result<FileMapView>> Handle(GetFileMapRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var repo = Addressed(request.Scope, query.Repo);
        var map = repo.IsError ? repo.Error : await reader.ReadView(request.Scope, query.Draft, cancellationToken).ConfigureAwait(false);
        var files = map.IsError ? map.Error : await Files(repo.Value, cancellationToken).ConfigureAwait(false);
        if (repo.IsError || map.IsError || files.IsError)
        {
            return repo.IsError ? repo.Error : map.IsError ? map.Error : files.Error;
        }

        return FileMapBuilder.Build(new FileMapBasis(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{map.Value.Snapshot.Version.Number}"), string.Empty), query, files.Value, map.Value.Graph);
    }

    private static Result<ContextRepo> Addressed(ContextScope scope, ContextRepoName name)
    {
        var addressed = SettingsReader.Read(scope.Settings).Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        if (addressed.IsError && addressed.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return addressed.Error;
        }

        var repo = addressed.IsError ? null : addressed.Value.FirstOrDefault(r => r.Name == name);
        return repo is null
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can read (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : repo;
    }

    private async ValueTask<Result<IReadOnlyList<string>>> Files(ContextRepo repo, CancellationToken cancellationToken)
    {
        var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
        var listed = head.IsError ? head.Error : await repos.ListFiles(repo, head.Value, cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyList<string>>(files => [.. files.Where(p => !repo.Denies(p))]);
    }
}
