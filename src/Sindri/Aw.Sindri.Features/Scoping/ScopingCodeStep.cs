using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// A scoping Step the block runtime runs (scoping spec §8.2, §9): the same code the desk helper runs (<see cref="DeskBlocks"/>), moved behind
/// <see cref="IBlockStep"/>, so a scoping instance passes the deterministic Steps without a desk task. The context's facts become the code's
/// facts (JSON text by key, without the 'fact:' prefix); the code's outputs become typed facts again. A subclass names its Step; one that needs
/// more than the facts (gather's Step inputs) overrides <see cref="Extra"/>. The desk's run-area files (rounds.jsonl, the packet) have no place
/// on the block runtime and are not written here.
/// </summary>
/// <param name="step">The Step's id, which picks the code.</param>
internal abstract class ScopingCodeStep(BlockId step) : IBlockStep
{
    private const string FactPrefix = "fact:";

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var extra = await Extra(context, cancellationToken).ConfigureAwait(false);
        if (extra.IsError)
        {
            return BlockStepResult.Failed(extra.Error.Message);
        }

        var ran = DeskBlocks.Run(step.Value, new ScopeFacts(Texts(context, extra.Value)));
        return ran.IsError ? BlockStepResult.Failed(ran.Error.Message) : Completed(ran.Value);
    }

    /// <summary>The code's facts beyond the context's own: what the Step's inputs gave it. None by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>JSON text by code fact key, or the error that fails the Step.</returns>
    protected virtual ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Facts([]));

    /// <summary>The JSON tree of one of the Step's facts.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="name">The fact's name without the 'fact:' prefix.</param>
    /// <returns>The tree, or null when the fact is absent or not JSON.</returns>
    protected static JsonNode? Fact(StepContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return context.Facts.TryGetValue(FactKey.From(FactPrefix + name), out var value) ? JsonNode.Parse(Text(value)) : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: a fact is text a model or a person wrote
        }
    }

    /// <summary>The 'contextQuery' fact the desk code takes: the text shape's suggested anchors.</summary>
    /// <param name="anchors">The anchors.</param>
    /// <returns>The fact's JSON text.</returns>
    protected static string ContextQuery(IEnumerable<string> anchors) => new JsonObject { ["suggestedAnchors"] = ScopeJson.ToArray(anchors) }.ToJsonString();

    /// <summary>The code's extra facts as a result.</summary>
    /// <param name="texts">JSON text by code fact key.</param>
    /// <returns>The result.</returns>
    protected static Result<IReadOnlyDictionary<string, string>> Facts(Dictionary<string, string> texts) => texts;

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

    private static Dictionary<string, string> Texts(StepContext context, IReadOnlyDictionary<string, string> extra)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context.Facts.Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal)))
        {
            texts[key.Value[FactPrefix.Length..]] = Text(value);
        }

        foreach (var (key, text) in extra)
        {
            texts[key] = text;
        }

        return texts;
    }
}
