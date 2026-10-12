using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RefreshDocs;

/// <summary>Refreshes every repo in scope in turn; one repo's failure is its row's code, not the whole refresh's.</summary>
internal sealed class RefreshDocsService(IDocIndex index) : IService<RefreshDocsRequest, RefreshReport>
{
    public async ValueTask<Result<RefreshReport>> Handle(RefreshDocsRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var repos = settings.Bind(s => ScopeRules.Resolve(s, request.Scope.Repos, request.Scope.Caller));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var rows = new List<RepoRefresh>();
        foreach (var repo in repos.Value)
        {
            var refreshed = await index.Refresh(repo, settings.Value.DocGlobs, cancellationToken).ConfigureAwait(false);
            if (refreshed.IsError)
            {
                return refreshed.Error;   // the store itself failed (SND-CONTEXT-004): no later repo can do better
            }

            rows.Add(refreshed.Value);
        }

        return new RefreshReport(RepoDocs.Id, rows);
    }
}
