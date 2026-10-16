using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>What 'workflow complete' knows about the outputs the engine fills (DT spec §8.1): which facts those are, from the build's descriptors, and how a decision is written.</summary>
/// <param name="catalog">The build's descriptors.</param>
/// <param name="json">Writes the engine-filled facts.</param>
internal sealed class EngineFilledReport(BlockCatalog catalog, JsonSerializerOptions json)
{
    /// <summary>The refusal for a report that writes an engine-filled output itself (SND-CLI-003), checked without reading the instance.</summary>
    /// <param name="written">The output keys the caller gave.</param>
    /// <returns>The refusal; null when none of them is engine-filled.</returns>
    public Error? Refusal(IEnumerable<FactKey> written) =>
        EngineFilledOutputs.Refusal(catalog.Blocks, written) is { } message ? SindriErrors.InvalidInput.With(message) : null;

    /// <summary>The caller's outputs and the engine-filled ones, written.</summary>
    /// <param name="given">The caller's outputs.</param>
    /// <param name="filled">The engine-filled decisions.</param>
    /// <returns>Every output's content.</returns>
    public Dictionary<FactKey, ReadOnlyMemory<byte>> Merge(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> given, IReadOnlyDictionary<FactKey, DecisionFact> filled)
    {
        var merged = new Dictionary<FactKey, ReadOnlyMemory<byte>>(given);
        foreach (var (fact, decision) in filled)
        {
            merged[fact] = AwJson.Serialize(decision, json);
        }

        return merged;
    }
}
