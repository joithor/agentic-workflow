using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Prove;

/// <summary>The phase packet's files the proof rules read (line spec §3.2 row 4): acceptance.json, signals.json, phase.json, holdout.json and packet.json.</summary>
/// <param name="Acceptance">acceptance.json.</param>
/// <param name="AcceptanceText">Its text, whose hash the proof pack's acceptance extends.</param>
/// <param name="Signals">signals.json.</param>
/// <param name="Kind">phase.json's kind, empty when it names none.</param>
/// <param name="HoldoutCount">holdout.json's count; null when it has none.</param>
/// <param name="Id">packet.json's id.</param>
/// <param name="Repo">packet.json's repo ('owner/name').</param>
/// <param name="BaseSha">packet.json's baseSha.</param>
internal sealed record PhaseFiles(JsonNode Acceptance, string AcceptanceText, JsonNode? Signals, string Kind, int? HoldoutCount, string Id, string Repo, string BaseSha)
{
    /// <summary>Reads the files from the phase packet's directory.</summary>
    /// <param name="files">The reader.</param>
    /// <param name="dir">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files, or the reason one could not be read.</returns>
    internal static async ValueTask<(PhaseFiles? Files, string? Failure)> Load(IBriefFiles files, string dir, CancellationToken cancellationToken)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "acceptance.json", "signals.json", "phase.json", "holdout.json", "packet.json" })
        {
            var read = await files.Read(name, [dir], cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return (null, $"{name}: {read.Error.Message}");
            }

            texts[name] = read.Value.Text;
        }

        return Parse(texts);
    }

    private static (PhaseFiles? Files, string? Failure) Parse(Dictionary<string, string> texts)
    {
        try
        {
            var signals = JsonNode.Parse(texts["signals.json"]);
            if (PhaseSignals.Check(signals) is { } wrongSignals)
            {
                return (null, wrongSignals);
            }

            var packet = JsonNode.Parse(texts["packet.json"]) as JsonObject;
            var phase = JsonNode.Parse(texts["phase.json"]) as JsonObject;
            var holdout = JsonNode.Parse(texts["holdout.json"]) as JsonObject;
            return packet?["id"] is null || packet["repo"] is null || packet["baseSha"] is null || JsonNode.Parse(texts["acceptance.json"]) is not JsonObject { } acceptance
                ? (null, "packet.json must name the packet's id, repo and baseSha, and acceptance.json must be an object")
                : (new PhaseFiles(acceptance, texts["acceptance.json"], signals, (string?)phase?["kind"] ?? string.Empty, (int?)holdout?["count"], (string)packet["id"]!, (string)packet["repo"]!, (string)packet["baseSha"]!), null); // why: the null checks above
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return (null, $"a phase packet file is not the JSON it should be: {e.Message}");
        }
    }
}
