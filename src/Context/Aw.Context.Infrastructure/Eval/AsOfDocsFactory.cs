using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Opens <see cref="AsOfRepoDocs"/> over git.</summary>
internal sealed class AsOfDocsFactory(GitCli git, TimeProvider clock) : IAsOfDocs
{
    public async ValueTask<Result<IContextSource>> Open(AsOfDocsRequest request, CancellationToken cancellationToken)
    {
        var docs = await AsOfRepoDocs.Build(request, git, clock, cancellationToken).ConfigureAwait(false);
        return docs.IsError ? docs.Error : Results.Success<IContextSource>(docs.Value);
    }
}
