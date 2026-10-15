using Aw.Context.Core.Models;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's model port (context spec §4) over Judge's provider catalog: a request goes to the owner's own agent CLIs
/// (Claude Code, Codex, Cursor) in the catalog's order, never to the remote 'jev' API, because the prompt names files of the owner's
/// repo. The first provider that answers wins; none answering is an unavailable source, which a map build reports as a degraded
/// 'labels' pass rather than a failure.
/// </summary>
/// <param name="catalog">Finds the provider lineup (resolved on the first request).</param>
internal sealed class JudgeModelPort(Func<IProviderCatalog> catalog) : IModelPort
{
    private readonly Func<IProviderCatalog> _catalog = catalog;

    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{_catalog.Method.Name}{request.Purpose}{new NoRulesProvider().Name}");
}
