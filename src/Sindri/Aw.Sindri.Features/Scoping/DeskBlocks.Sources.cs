using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The readers of a tracker item and of a change (scoping spec §4): 'read-tracker' and 'read-change' take what the Step fetched through its port as one source fact
/// (key, title, text, version, fetchedAt; the text already scrubbed) and the text shape's answer (contextQuery). They run on the block runtime only: the desk helper does not cover them.
/// </summary>
internal static partial class DeskBlocks
{
    private static Result<ScopingDeskResponse> ReadTracker(ScopeFacts facts) => ReadSource(facts, "step.scope.read-tracker", "tracker", "trackerSource");

    private static Result<ScopingDeskResponse> ReadChange(ScopeFacts facts) => ReadSource(facts, "step.scope.read-change", "change", "changeSource");

    private static Result<ScopingDeskResponse> ReadSource(ScopeFacts facts, string block, string kind, string sourceFact)
    {
        var noun = block["step.scope.".Length..];
        if (facts.Missing("scopeInput", sourceFact, "contextQuery") is { Count: > 0 })
        {
            return Needs(noun, facts, "(fix: they are what the Step fetched through its port and the text shape's answer)", "scopeInput", sourceFact, "contextQuery");
        }

        if (!string.Equals(ScopeJson.Str(facts["scopeInput"], "kind"), kind, StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"{noun} reads a {kind}; fact:scopeInput has the kind '{ScopeJson.Str(facts["scopeInput"], "kind") ?? "none"}' (fix: use the reader for that kind)");
        }

        var source = facts.Require(sourceFact);
        return string.IsNullOrWhiteSpace(ScopeJson.Str(source, "text")) ? Reply(block, "empty", Outputs(), []) : Brief(block, kind, source, facts);
    }

    private static Result<ScopingDeskResponse> Brief(string block, string kind, JsonNode source, ScopeFacts facts)
    {
        var anchors = Suggested(facts["contextQuery"]);
        if (anchors.Error is { } error)
        {
            return error;
        }

        var reference = new JsonObject
        {
            ["id"] = "R1",
            ["source"] = kind,
            ["key"] = ScopeJson.Str(source, "key") ?? string.Empty,
            ["version"] = ScopeJson.Str(source, "version") ?? string.Empty,
            ["trust"] = "untrusted",
            ["fetchedAt"] = ScopeJson.Str(source, "fetchedAt") ?? string.Empty,
        };
        var brief = new JsonObject { ["text"] = ScopeJson.Str(source, "text"), ["refs"] = new JsonArray(reference), ["subject"] = ScopeJson.Str(source, "title") ?? kind };
        return WithWarnings(block, "read", Outputs(("brief", brief), ("anchors", ScopeJson.ToArray(anchors.Anchors))), anchors.Warnings);
    }
}
