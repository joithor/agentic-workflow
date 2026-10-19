using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Features;

/// <summary>
/// A design or plan station Step the block runtime runs (line spec §4.1, §6.1): the context's facts become <see cref="LineFacts"/> (JSON by key, without the
/// 'fact:' prefix), the subclass decides, and the verdict's facts become typed facts again. A fact the descriptor reads and the instance does not hold,
/// or one that is not JSON, fails the Step: a check that read a missing fact as a rule that did not run must never pass.
/// </summary>
/// <param name="step">The Step's descriptor (its inputs are the facts it needs).</param>
internal abstract class LineStep(BlockDescriptor step) : IBlockStep
{
    private const string FactPrefix = "fact:";

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(RunSync(context));
    }

    /// <summary>The Step's decision over its facts.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The verdict, or why the Step fails.</returns>
    protected abstract Result<LineVerdict> Decide(LineFacts facts);

    private static JsonElement Element(JsonNode node)
    {
        using var parsed = JsonDocument.Parse(LineJson.Write(node));
        return parsed.RootElement.Clone();
    }

    private static Result<JsonNode?> Parse(string key, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
        try
        {
            return Results.Success(JsonNode.Parse(text));
        }
        catch (JsonException e)
        {
            return SindriErrors.InvalidInput.With($"{key} is not JSON ({e.Message})");   // boundary: a fact is text a model or a person wrote
        }
    }

    private BlockStepResult RunSync(StepContext context)
    {
        if (step.Inputs.Where(k => !context.Facts.ContainsKey(k)).Select(static k => k.Value).ToList() is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"{step.Id.Value} needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var nodes = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in context.Facts.Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal)))
        {
            var parsed = Parse(key.Value, value);
            if (parsed.IsError)
            {
                return BlockStepResult.Failed(parsed.Error.Message);
            }

            nodes[key.Value[FactPrefix.Length..]] = parsed.Value;
        }

        var verdict = Decide(new LineFacts(nodes));
        return verdict.IsError
            ? BlockStepResult.Failed(verdict.Error.Message)
            : BlockStepResult.Completed(OutcomeName.From(verdict.Value.Outcome), verdict.Value.Outputs.ToDictionary(static o => FactKey.From(FactPrefix + o.Key), static o => Element(o.Value)));
    }
}
