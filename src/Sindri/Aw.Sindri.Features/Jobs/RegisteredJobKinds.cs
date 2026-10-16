using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>The kinds of the jobs AddJob registered: each definition's request, looked up in the command catalog.</summary>
/// <param name="definitions">Every registered job definition.</param>
/// <param name="catalog">The command catalog.</param>
internal sealed class RegisteredJobKinds(IEnumerable<JobDefinition> definitions, ICommandCatalog catalog) : IJobKinds
{
    public IReadOnlyList<JobKind> Known { get; } = [.. definitions.SelectMany(d => catalog.For(d.Request)).Select(static r => r.Action.Value)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(JobKind.From)];
}
