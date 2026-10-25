using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The frozen scope packet as the design checks read it (line spec §4.1 'intake'): verified against the hash the fact names, then 'fact:scope' =
/// { hash, dir, baseSha, change, scopeMap, acceptance, acceptanceHash, openQuestions[], answerLog[] }. An open question is one of questions.json that no answer batch
/// of answers.jsonl answered; acceptanceHash is the digest of acceptance.json's bytes, which the design's acceptance chain extends (LC-2).
/// </summary>
internal static class LineScope
{
    private static readonly string[] _objectFiles = ["scope-map.json", "acceptance.json", "questions.json"];

    /// <summary>Verifies the scope packet and loads it.</summary>
    /// <param name="packets">aw-packets.</param>
    /// <param name="files">The packet's files.</param>
    /// <param name="scopePacket">The 'fact:scopePacket' tree.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 'fact:scope' object; why the packet cannot be used.</returns>
    internal static async ValueTask<Result<JsonObject>> Load(IPackets packets, IPacketFiles files, JsonNode? scopePacket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(files);
        var fact = PacketFact.Of(scopePacket, "scopePacket");
        if (fact.IsError)
        {
            return fact.Error;
        }

        var verified = await LinePackets.Verify(packets, PacketKind.Scoping, fact.Value, cancellationToken).ConfigureAwait(false);
        if (verified.IsError)
        {
            return verified.Error;
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in _objectFiles.Append("answers.jsonl"))
        {
            var read = await files.Read(fact.Value.Directory, name, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            texts[name] = read.Value;
        }

        return Assemble(fact.Value, texts);
    }

    private static Result<JsonObject> Assemble(PacketFact packet, Dictionary<string, string> texts)
    {
        var parsed = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var name in _objectFiles)
        {
            if (Parse(texts[name]) is not JsonObject obj)
            {
                return SindriErrors.InvalidInput.With($"the scope packet's {name} is not a JSON object (fix: re-run scoping; a packet is never edited)");
            }

            parsed[name] = obj;
        }

        var log = new JsonArray();
        foreach (var line in texts["answers.jsonl"].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            LineJson.Append(log, Parse(line) ?? new JsonObject());
        }

        var answered = LineJson.Items(log).SelectMany(static a => LineJson.Strings(a, "questionIds")).ToHashSet(StringComparer.Ordinal);
        var open = new JsonArray();
        foreach (var question in LineJson.Items(parsed["questions.json"], "questions").Where(q => LineJson.Str(q, "id") is { } id && !answered.Contains(id)))
        {
            LineJson.Append(open, question.DeepClone());
        }

        return new JsonObject
        {
            ["hash"] = packet.Hash,
            ["dir"] = packet.Directory,
            ["baseSha"] = LineJson.Str(parsed["scope-map.json"], "baseSha"),
            ["change"] = LineJson.Str(parsed["scope-map.json"], "change"),
            ["scopeMap"] = parsed["scope-map.json"],
            ["acceptance"] = parsed["acceptance.json"],
            ["acceptanceHash"] = LinePackets.Sha256(texts["acceptance.json"]),
            ["openQuestions"] = open,
            ["answerLog"] = log,
        };
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;   // boundary: a packet file is text a model wrote
        }
    }
}
