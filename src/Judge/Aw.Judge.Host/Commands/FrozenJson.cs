using System.Globalization;
using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>The frozen JSON shapes the TS judge printed: its key names, key order and value forms.</summary>
internal static class FrozenJson
{
    /// <summary>A brief (TS db.ts BriefRow, camelCase in column order).</summary>
    internal static JsObject Brief(Brief brief) => new JsObject()
        .Set("toolUseId", JsValue.From(brief.ToolUseId.Value))
        .Set("agentId", JsValue.FromNullable(brief.AgentId?.Value))
        .Set("sessionId", JsValue.From(brief.SessionId.Value))
        .Set("promptId", JsValue.From(brief.PromptId.Value))
        .Set("dispatchName", JsValue.FromNullable(brief.DispatchName))
        .Set("subagentType", JsValue.From(brief.SubagentType))
        .Set("goal", JsValue.From(brief.Goal))
        .Set("acceptanceCriteria", JsValue.From(brief.AcceptanceCriteria))
        .Set("proofCommand", JsValue.From(brief.ProofCommand))
        .Set("savedAt", JsValue.From(Timestamp(brief.SavedAt)));

    /// <summary>JavaScript's Date.prototype.toISOString: UTC, milliseconds, 'Z'.</summary>
    internal static string Timestamp(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
