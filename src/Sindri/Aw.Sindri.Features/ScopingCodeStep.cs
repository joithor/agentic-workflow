using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// A scoping Step the block runtime runs (scoping spec §8.2, §9): the same code the desk helper runs (<see cref="DeskBlocks"/>), moved behind
/// <see cref="IBlockStep"/>, so a scoping instance passes the deterministic Steps without a desk task. The context's facts become the code's
/// facts (JSON text by key, without the 'fact:' prefix); the code's outputs become typed facts again. A subclass names its Step; one that needs
/// more than the facts (gather's Step inputs) overrides <see cref="Extra"/>. The desk's run-area files (rounds.jsonl, the packet) have no place
/// on the block runtime and are not written here.
/// </summary>
/// <param name="step">The Step's descriptor (its id picks the code).</param>
internal abstract class ScopingCodeStep(BlockDescriptor step) : IBlockStep
{
    private const string FactPrefix = "fact:";

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (step.Inputs.Where(k => !context.Facts.ContainsKey(k)).Select(static k => k.Value).ToList() is { Count: > 0 } missing)
        {
            return ValueTask.FromResult(BlockStepResult.Failed($"{step.Id.Value} needs {string.Join(", ", missing)}, which the instance does not hold"));   // the checks read a missing fact as a rule that did not run, which must never pass a Step
        }

        var ran = DeskBlocks.Run(step.Id.Value, new ScopeFacts(Texts(context)));
        return ValueTask.FromResult(ran.IsError ? BlockStepResult.Failed(ran.Error.Message) : Completed(ran.Value));
    }

    /// <summary>The code's facts beyond the context's own: what the Step's inputs gave it. None by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>JSON text by code fact key.</returns>
    protected virtual IReadOnlyDictionary<string, string> Extra(StepContext context) => new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The JSON text a fact or an input's typed result holds: the string the runner stored, or the element's own text.</summary>
    /// <param name="value">The element.</param>
    /// <returns>The text.</returns>
    protected static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private static BlockStepResult Completed(ScopingDeskResponse response)
    {
        var outputs = response.Outputs.ToDictionary(static o => FactKey.From(FactPrefix + o.Key), static o => Element(o.Value));
        return BlockStepResult.Completed(OutcomeName.From(response.Outcome), outputs);
    }

    // A fact is stored as its text, so a JSON string fact goes back as a string element holding its quoted text and everything else as its own value.
    private static JsonElement Element(string text)
    {
        using var parsed = JsonDocument.Parse(text);
        if (parsed.RootElement.ValueKind != JsonValueKind.String)
        {
            return parsed.RootElement.Clone();
        }

        using var quoted = JsonDocument.Parse(ScopeJson.Write(JsonValue.Create(text) ?? throw new InvalidOperationException("a string is always a value"))); // why: Create returns null only for a null string, and text is not null
        return quoted.RootElement.Clone();
    }

    private Dictionary<string, string> Texts(StepContext context)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context.Facts.Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal)))
        {
            texts[key.Value[FactPrefix.Length..]] = Text(value);
        }

        foreach (var (key, text) in Extra(context))
        {
            texts[key] = text;
        }

        return texts;
    }
}
