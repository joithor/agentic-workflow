using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Sindri.Host.Models;

/// <summary>The rules provider Judge's lineup closes its chains with: labeling has no rule to fall back on, so it never decides.</summary>
internal sealed class NoRulesProvider : IModelProvider
{
    public ProviderName Name => ProviderName.Rules;

    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>();

    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ProviderResult>(new ProviderUnavailable("no-rule"));
}
