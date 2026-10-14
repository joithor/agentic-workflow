using Aw.Kernel;

namespace Aw.Context.Core.Models;

/// <summary>
/// Every model call the engine makes (labeling, decision extraction, rerank, adjudication) goes through this port
/// (spec §4, README X18). Sindri's Host adapts it over Judge's provider port, so the provider (a CLI or an API key) is a
/// profile choice and an adapter, never an interface change. No model is called on the query path unless rerank is on.
/// </summary>
public interface IModelPort
{
    /// <summary>Asks the model.</summary>
    /// <param name="request">The scrubbed prompt, purpose and limits.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer, or an error (Unavailable, Timeout, BudgetExceeded).</returns>
    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken);
}
