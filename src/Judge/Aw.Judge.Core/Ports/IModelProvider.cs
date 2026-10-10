using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>A model (or rule) provider in the chain: answers one question about one input.</summary>
public interface IModelProvider
{
    /// <summary>Which provider this is.</summary>
    public ProviderName Name { get; }

    /// <summary>The content classes it can judge.</summary>
    public IReadOnlySet<ContentClass> Classes { get; }

    /// <summary>
    /// Asks the question. A provider that runs past <paramref name="budget"/> stops and returns
    /// <see cref="ProviderUnavailable"/> with reason <see cref="ProviderUnavailable.Timeout"/>; a throw is recorded as
    /// 'provider-threw' and the chain moves on.
    /// </summary>
    /// <param name="question">The question, with the prompt built for this input.</param>
    /// <param name="input">The validated input (JSON null as null).</param>
    /// <param name="budget">The question's time budget.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer.</returns>
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken);
}
