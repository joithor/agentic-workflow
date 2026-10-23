using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The facts a Step declares as inputs, shaped as the files its run directory gets before the session starts.</summary>
internal static class StepFactFiles
{
    private const string FactPrefix = "fact:";

    /// <summary>The file name to content of each declared read fact that is set.</summary>
    /// <param name="descriptor">The Step's descriptor; null when the catalog doesn't know it (no files).</param>
    /// <param name="facts">The instance's stored facts.</param>
    /// <param name="contents">Reads the stored content.</param>
    /// <param name="scrubber">Removes secrets from the text.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The files; an error when a set fact's content can't be read.</returns>
    internal static async ValueTask<Result<IReadOnlyDictionary<string, string>>> Build(BlockDescriptor? descriptor, IReadOnlyDictionary<FactKey, FactValue>? facts, IFactContents contents, ITextScrubber scrubber, CancellationToken cancellationToken)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (var key in descriptor?.Inputs ?? [])
        {
            if (facts is null || !facts.TryGetValue(key, out var stored))
            {
                continue;   // declared, not set yet: no file
            }

            var bytes = await contents.Read(stored, cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            files[Name(key) + ".json"] = Shaped(scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span)));
        }

        return files;
    }

    private static string Name(FactKey key) => key.Value.StartsWith(FactPrefix, StringComparison.Ordinal) ? key.Value[FactPrefix.Length..] : key.Value;

    /// <summary>What a fact read serves (GetInstanceFactService): stored JSON that is an object or an array as itself, anything else as one JSON string. Never fenced: the file is data for a CLI, not prompt text.</summary>
    private static string Shaped(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is { } node && node is JsonObject or JsonArray)
            {
                return node.ToJsonString();
            }
        }
        catch (JsonException)
        {
            // boundary: stored content that isn't JSON is a text fact
        }

        return JsonValue.Create(text)!.ToJsonString();   // why: a string is never null
    }
}
