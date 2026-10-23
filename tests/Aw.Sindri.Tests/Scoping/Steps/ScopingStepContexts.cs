using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>Builds a block Step's context the way the 'RunBlockStep' activity does: each fact as the string element of its stored JSON text.</summary>
internal static class ScopingStepContexts
{
    internal static StepContext Of(string step, ScopingFacts facts, IReadOnlyDictionary<string, JsonElement>? inputs = null, IReadOnlyDictionary<string, int>? loopCaps = null) => new(
        BlockId.From(step),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        facts.ToDictionary(static f => FactKey.From("fact:" + f.Key), static f => JsonSerializer.SerializeToElement(f.Value)),
        InjectionSections.Empty,
        inputs)
    {
        Address = Address,
        LoopCaps = loopCaps ?? new Dictionary<string, int>(StringComparer.Ordinal),
    };

    /// <summary>The instance the contexts name.</summary>
    internal const string Address = "T-1/scoping/1";

    /// <summary>The fact keys of a result's outputs, without the prefix, in order.</summary>
    internal static string[] Keys(BlockStepResult result) => [.. result.Result!.Outputs.Keys.Select(static k => k.Value["fact:".Length..]).Order(StringComparer.Ordinal)];
}
