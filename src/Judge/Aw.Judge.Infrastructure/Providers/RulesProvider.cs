using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The chain's last resort, 'rules'. It decides nothing itself: a question's pre-rules and fallback rules run in the
/// evaluation engine (pre-rules before the chain, fallbacks on a below-threshold answer), so here it is always
/// unavailable ('no-rule', not a failure). It stays in the chain so every class ends with rules.
/// </summary>
internal sealed class RulesProvider : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => ProviderName.Rules;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red");
}
