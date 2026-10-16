using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>
/// The scrubbed body behind one fact row (API spec §3.8): the instance's view names the declared facts and their stored digests, the
/// fact store reads the content, the scrubber removes secrets, and the body is capped at <see cref="CapBytes"/>. A text fact is a
/// string; any other type is its stored JSON, which stays an object when cut (it loses its tail entries). A key the recipe doesn't
/// declare, or one not produced yet, answers SND-ITEM-404, as does another repo's instance for a session caller.
/// </summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
/// <param name="repos">Where each item belongs, from the ledger.</param>
/// <param name="callers">The caller, whose repo scope decides what it may read.</param>
internal sealed class GetInstanceFactService(IInstanceDirectory directory, IInstanceReader runtime, IFactContents contents, ITextScrubber scrubber, IItemRepos repos, ICallerContextAccessor callers)
    : IService<GetInstanceFactRequest, FactView>
{
    /// <summary>The largest body served: 256 KiB of UTF-8.</summary>
    internal const int CapBytes = 256 * 1024;

    public async ValueTask<Result<FactView>> Handle(GetInstanceFactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a fact read needs a caller");
        }

        var address = await directory.Resolve(request.Instance.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (await InstanceScope.Refusal(caller, view.Value.ItemId, repos, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var row = view.Value.Facts.FirstOrDefault(f => string.Equals(f.Key, request.Key, StringComparison.Ordinal));
        if (row is null || view.Value.FactShas?.GetValueOrDefault(request.Key) is not { } sha)
        {
            return SindriErrors.ItemNotFound.With(row is null ? $"no fact '{request.Key}' on the instance" : $"the fact '{request.Key}' is not produced yet");
        }

        var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        return bytes.IsError ? bytes.Error : Body(row, scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span)));
    }

    private static FactView Body(FactRow row, string text)
    {
        if (row.Type != FactType.Text && Parse(text) is { } node)
        {
            var (cut, truncated) = Cap(node);
            return new FactView(row.Key, row.Type, Element(cut), truncated);
        }

        var (kept, clipped) = CutText(text);
        return new FactView(row.Key, row.Type, Element(JsonValue.Create(kept)), clipped);
    }

    private static JsonElement Element(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    /// <summary>The stored text as JSON when it is an object or an array; null otherwise (it is then served as a string).</summary>
    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) is { } node && node is JsonObject or JsonArray ? node : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: stored content that isn't JSON is served as text
        }
    }

    /// <summary>The node cut to the cap by dropping its tail entries; unchanged when it fits.</summary>
    private static (JsonNode Node, bool Truncated) Cap(JsonNode node)
    {
        if (Size(node) <= CapBytes)
        {
            return (node, false);
        }

        switch (node)
        {
            case JsonObject obj:
                var keys = obj.Select(static p => p.Key).ToList();
                var copy = new JsonObject();
                foreach (var key in keys)
                {
                    copy[key] = obj[key]?.DeepClone();
                    if (Size(copy) > CapBytes)
                    {
                        _ = copy.Remove(key);
                        break;
                    }
                }

                return (copy, true);
            default:
                var array = (JsonArray)node;
                var kept = new JsonArray();
                foreach (var item in array)
                {
                    kept.Add(item?.DeepClone());
                    if (Size(kept) > CapBytes)
                    {
                        kept.RemoveAt(kept.Count - 1);
                        break;
                    }
                }

                return (kept, true);
        }
    }

    private static int Size(JsonNode node) => Encoding.UTF8.GetByteCount(node.ToJsonString());

    /// <summary>The text cut to the cap on a character boundary.</summary>
    private static (string Text, bool Truncated) CutText(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= CapBytes)
        {
            return (text, false);
        }

        var length = Math.Min(text.Length, CapBytes);
        while (Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > CapBytes || (length > 0 && char.IsHighSurrogate(text[length - 1])))
        {
            length--;
        }

        return (text[..length], true);
    }
}
