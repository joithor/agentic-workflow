using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>A code search that resolves a commit as given and never answers a search, so the profile's deadline decides the lexical rows (the deadline-152 golden).</summary>
internal sealed class HangingCodeSearch : ICodeSearch
{
    /// <summary>Replaces the host's code search with this one.</summary>
    /// <param name="services">The host's services.</param>
    internal static void Install(IServiceCollection services)
    {
        _ = services.RemoveAll<ICodeSearch>();
        _ = services.AddSingleton<ICodeSearch>(new HangingCodeSearch());
    }

    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(commit);

    public async ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return new CodeSearchHits([], Truncated: false);
    }
}
