using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>Stub: the boundary backstop around one adapter.</summary>
/// <param name="inner">The adapter.</param>
internal sealed class FailOpenModelProvider(IModelProvider inner) : IModelProvider
{
    /// <summary>The adapter it wraps.</summary>
    public IModelProvider Inner { get; } = inner;

    /// <inheritdoc />
    public ProviderName Name => Inner.Name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes => Inner.Classes;

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken) =>
        Inner.Decide(question, input, budget, cancellationToken);
}
