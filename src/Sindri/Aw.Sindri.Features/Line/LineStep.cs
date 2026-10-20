using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// A design or plan station Step the block runtime runs (line spec §4.1, §6.1): the context's facts become <see cref="LineFacts"/> (JSON by key, without the
/// 'fact:' prefix), the subclass decides, and the verdict's facts become typed facts again.
/// The runner has failed the Step already when a fact its descriptor reads is missing; a fact read as a structure that is not JSON fails it here: a check that read garbage as empty must never pass.
/// </summary>
/// <param name="step">The Step's id.</param>
internal abstract class LineStep(BlockId step) : IBlockStep
{
    private const string FactPrefix = "fact:";

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(RunSync(context));
    }

    /// <summary>The facts (without the 'fact:' prefix) the Step reads as a JSON object or array; text that is not JSON there fails the Step instead of reading as empty. None by default.</summary>
    protected virtual IReadOnlyList<string> Structured => [];

    /// <summary>The Step's decision over its facts.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The verdict, or why the Step fails.</returns>
    protected abstract Result<LineVerdict> Decide(LineFacts facts);

    private static JsonElement Element(JsonNode node)
    {
        using var parsed = JsonDocument.Parse(LineJson.Write(node));
        return parsed.RootElement.Clone();
    }

    // A fact is JSON text; a scalar that is not JSON (a packet hash such as sha256:ab12) stays the string it is. A fact a Step reads as a structure is checked in RunSync.
    private static JsonNode? Parse(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);   // boundary: a fact is text a model or a person wrote
        }
    }

    private BlockStepResult RunSync(StepContext context)
    {
        var nodes = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in context.Facts.Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal)))
        {
            nodes[key.Value[FactPrefix.Length..]] = Parse(value);
        }

        if (Structured.Where(k => nodes.GetValueOrDefault(k) is not (JsonObject or JsonArray)).ToList() is { Count: > 0 } broken)
        {
            return BlockStepResult.Failed($"{step.Value} reads {string.Join(", ", broken.Select(static k => FactPrefix + k))} as JSON, and the instance holds text that is not (fix: the Step that wrote it must write a JSON object or array)");
        }

        var verdict = Decide(new LineFacts(nodes));
        return verdict.IsError
            ? BlockStepResult.Failed(verdict.Error.Message)
            : BlockStepResult.Completed(OutcomeName.From(verdict.Value.Outcome), verdict.Value.Outputs.ToDictionary(static o => FactKey.From(FactPrefix + o.Key), static o => Element(o.Value)));
    }
}
